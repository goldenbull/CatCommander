using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Services;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

public enum ItemBrowserViewMode
{
    List,
    TreeList,
}

/// <summary>
/// One tab's content: address bar + file list/tree + selection summary. Talks to whatever
/// IFileSystemProvider FileSystemProviderRegistry resolves CurrentPath to - currently always
/// LocalFileSystemProvider, but nothing here assumes that.
/// </summary>
[Observable]
public partial class ItemBrowserViewModel : IShortcutCommandSource
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly FileSystemProviderRegistry _providers;
    private readonly IconCache _iconCache;
    private readonly Dictionary<Operation, ICommand> _commands;

    private IFileSystemProvider? _provider;
    private IReadOnlyList<IFileSystemItem> _allItems = Array.Empty<IFileSystemItem>();

    public string CurrentPath { get; set; } = string.Empty;
    public ItemBrowserViewMode ViewMode { get; set; } = ItemBrowserViewMode.List;
    public ITreeDataGridSource? Source { get; private set; }

    public int TotalFileCount { get; private set; }
    public int TotalFolderCount { get; private set; }
    public long TotalSize { get; private set; }
    public int SelectedFileCount { get; private set; }
    public int SelectedFolderCount { get; private set; }
    public long SelectedSize { get; private set; }

    public string SelectionSummary =>
        $"Selected {FileItemModel.FormatFileSize(SelectedSize)} / {FileItemModel.FormatFileSize(TotalSize)}, " +
        $"{SelectedFileCount} / {TotalFileCount} files, {SelectedFolderCount} / {TotalFolderCount} folders";

    public ICommand ToggleViewModeCommand { get; }

    public ItemBrowserViewModel(FileSystemProviderRegistry providers, IconCache iconCache)
    {
        _providers = providers;
        _iconCache = iconCache;

        ToggleViewModeCommand = ReactiveCommand.Create(ToggleViewMode);

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.GoIntoCurrentFolder] = ReactiveCommand.Create(GoIntoCurrentFolder),
            [Operation.GoBackToParentFolder] = ReactiveCommand.Create(GoBackToParentFolder),
            [Operation.GotoFirstItem] = ReactiveCommand.Create(GotoFirstItem),
            [Operation.GotoLastItem] = ReactiveCommand.Create(GotoLastItem),
        };
    }

    public async Task NavigateToAsync(string path)
    {
        try
        {
            var (provider, relativePath) = await _providers.ResolveAsync(path);
            var items = await provider.ListChildrenAsync(relativePath);

            _provider = provider;
            CurrentPath = path;
            _allItems = Sort(items);

            RebuildSource();
            RecomputeTotals();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to navigate to {0}", path);
        }
    }

    private static IReadOnlyList<IFileSystemItem> Sort(IReadOnlyList<IFileSystemItem> items) =>
        items
            .OrderByDescending(i => i.ItemType == FileSystemItemType.Directory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void RebuildSource()
    {
        (Source as IDisposable)?.Dispose();
        Source = ViewMode == ItemBrowserViewMode.List ? BuildFlatSource() : BuildHierarchicalSource();
    }

    private FlatTreeDataGridSource<FileItemRow> BuildFlatSource()
    {
        var source = new FlatTreeDataGridSource<FileItemRow>(BuildRows(_allItems, includeParentRow: true))
        {
            Columns =
            {
                CreateNameColumn(),
                new TextColumn<FileItemRow, string>("Ext", x => x.Item.Extension, new GridLength(80)),
                new TextColumn<FileItemRow, string>("Size", x => x.Item.DisplaySize, new GridLength(100)),
                new TextColumn<FileItemRow, DateTime>("Modified", x => x.Item.Modified, new GridLength(150)),
            },
        };
        source.RowSelection!.SingleSelect = false;
        source.RowSelection!.SelectionChanged += OnSelectionChanged;
        return source;
    }

    private HierarchicalTreeDataGridSource<FileItemRow> BuildHierarchicalSource()
    {
        var nameColumn = new HierarchicalExpanderColumn<FileItemRow>(
            CreateNameColumn(),
            ChildSelector,
            hasChildrenSelector: x => x.Item.ItemType == FileSystemItemType.Directory);

        var source = new HierarchicalTreeDataGridSource<FileItemRow>(BuildRows(_allItems, includeParentRow: false))
        {
            Columns =
            {
                nameColumn,
                new TextColumn<FileItemRow, string>("Ext", x => x.Item.Extension, new GridLength(80)),
                new TextColumn<FileItemRow, string>("Size", x => x.Item.DisplaySize, new GridLength(100)),
                new TextColumn<FileItemRow, DateTime>("Modified", x => x.Item.Modified, new GridLength(150)),
            },
        };
        source.RowSelection!.SingleSelect = false;
        source.RowSelection!.SelectionChanged += OnSelectionChanged;
        return source;
    }

    // Synchronous bridge over the async provider - acceptable for local I/O (see design notes),
    // but tree-list mode needs real async lazy-loading before a remote provider can use it.
    private IEnumerable<FileItemRow>? ChildSelector(FileItemRow row)
    {
        if (row.Item.ItemType != FileSystemItemType.Directory || _provider is null)
            return null;

        try
        {
            var children = _provider.ListChildrenAsync(row.Item.FullPath).GetAwaiter().GetResult();
            return BuildRows(Sort(children), includeParentRow: false);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to expand {0}", row.Item.FullPath);
            return null;
        }
    }

    private List<FileItemRow> BuildRows(IReadOnlyList<IFileSystemItem> items, bool includeParentRow)
    {
        var rows = new List<FileItemRow>();

        if (includeParentRow)
        {
            var parentPath = Path.GetDirectoryName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar));
            if (!string.IsNullOrEmpty(parentPath))
            {
                rows.Add(new FileItemRow(
                    new FileItemModel { Name = "..", FullPath = parentPath, ItemType = FileSystemItemType.Special },
                    _iconCache));
            }
        }

        rows.AddRange(items.Select(i => new FileItemRow(i, _iconCache)));
        return rows;
    }

    private static TemplateColumn<FileItemRow> CreateNameColumn()
    {
        var template = new FuncDataTemplate<FileItemRow>((row, _) =>
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

            var image = new Image { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
            image.Bind(Image.SourceProperty, new Binding(nameof(FileItemRow.Icon)));

            var text = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            text.Bind(TextBlock.TextProperty, new Binding($"{nameof(FileItemRow.Item)}.{nameof(IFileSystemItem.Name)}"));

            panel.Children.Add(image);
            panel.Children.Add(text);
            return panel;
        });

        return new TemplateColumn<FileItemRow>("Name", template, width: GridLength.Star);
    }

    private void OnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileItemRow> e) =>
        RecomputeSelection();

    private void RecomputeTotals()
    {
        TotalFileCount = _allItems.Count(i => i.ItemType == FileSystemItemType.File);
        TotalFolderCount = _allItems.Count(i => i.ItemType == FileSystemItemType.Directory);
        TotalSize = _allItems.Where(i => i.ItemType == FileSystemItemType.File).Sum(i => i.Size);
        RecomputeSelection();
    }

    private void RecomputeSelection()
    {
        var selected = SelectionModel?.SelectedItems.Where(x => x is not null).Select(x => x!.Item).ToList()
            ?? new List<IFileSystemItem>();

        SelectedFileCount = selected.Count(i => i.ItemType == FileSystemItemType.File);
        SelectedFolderCount = selected.Count(i => i.ItemType == FileSystemItemType.Directory);
        SelectedSize = selected.Where(i => i.ItemType == FileSystemItemType.File).Sum(i => i.Size);
    }

    private TreeDataGridRowSelectionModel<FileItemRow>? SelectionModel =>
        Source?.Selection as TreeDataGridRowSelectionModel<FileItemRow>;

    private void ToggleViewMode()
    {
        ViewMode = ViewMode == ItemBrowserViewMode.List ? ItemBrowserViewMode.TreeList : ItemBrowserViewMode.List;
        RebuildSource();
    }

    private void GoIntoCurrentFolder()
    {
        var selected = SelectionModel?.SelectedItem;
        if (selected is not null && _provider is not null && _provider.CanEnter(selected.Item))
            _ = NavigateToAsync(selected.Item.FullPath);
    }

    private void GoBackToParentFolder()
    {
        var parent = Path.GetDirectoryName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar));
        if (!string.IsNullOrEmpty(parent))
            _ = NavigateToAsync(parent);
    }

    private void GotoFirstItem()
    {
        if (Source?.Rows.Count > 0)
            SelectionModel?.Select(new IndexPath(0));
    }

    private void GotoLastItem()
    {
        var count = Source?.Rows.Count ?? 0;
        if (count > 0)
            SelectionModel?.Select(new IndexPath(count - 1));
    }

    public ICommand? GetCommand(Operation operation) => _commands.GetValueOrDefault(operation);
}
