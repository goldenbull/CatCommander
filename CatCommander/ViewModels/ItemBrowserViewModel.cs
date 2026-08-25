using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using CatCommander.Utils;
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

    /// <summary>
    /// The current listing's root-level rows (one FileItemRow per _allItems entry). Kept alive
    /// across filter changes so ApplyFilter can just pick a subset of these into Source.Items
    /// rather than rebuilding rows from scratch (which would re-trigger every FileItemRow's async
    /// icon load per keystroke). Rebuilt only by RebuildSource - i.e. on navigation and view-mode
    /// toggles.
    /// </summary>
    private List<FileItemRow> _rows = new();

    public string CurrentPath { get; set; } = string.Empty;
    public ItemBrowserViewMode ViewMode { get; set; } = ItemBrowserViewMode.List;
    public ITreeDataGridSource<FileItemRow>? Source { get; private set; }

    /// <summary>
    /// Total Commander-style quick filter text - see ApplyFilter. Reset to empty by every
    /// RebuildSource (a new folder listing starts unfiltered); otherwise only ever changed via
    /// AppendFilterText/RemoveLastFilterCharacter/ClearFilter, called by the View in response to
    /// characters typed while FileGrid has focus (see ItemBrowser.axaml.cs).
    /// </summary>
    public string FilterText { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the quick filter bar should be shown below the grid - see ItemBrowser.axaml.
    /// Maintained explicitly alongside FilterText (in SetFilterText) rather than as a computed
    /// FilterText.Length > 0 property - Metalama's Observable aspect wasn't reliably raising
    /// PropertyChanged for a get-only property derived from a private-set one across a keystroke
    /// burst (the bar never appeared despite FilterText being set), so this avoids depending on
    /// that dependency inference for something the UI actually needs to react to.
    /// </summary>
    public bool IsFilterActive { get; private set; }

    /// <summary>
    /// Paths visited via NavigateToAsync, most-recent-first, backing the address bar's history
    /// dropdown. Only recorded when the resolved provider's TracksHistory says so (real,
    /// independently-typeable roots - the local file system, later an SFTP session) - a future
    /// archive provider's path-inside-an-archive navigation, or Ctrl+B's in-place tree expansion
    /// (which doesn't call NavigateToAsync at all), never lands here. Revisiting a path moves it
    /// back to the front rather than duplicating it.
    /// </summary>
    public ObservableCollection<string> NavigationHistory { get; } = new();

    private const int MaxHistoryEntries = 50;

    /// <summary>
    /// Whether this is MainPanelViewModel.ActiveTab right now - set by MainPanelViewModel.SetActiveTab,
    /// not computed locally, same reasoning as MainPanelViewModel.IsActive. Drives the tab-header
    /// button's selected look in MainPanel's View.
    /// </summary>
    public bool IsActiveTab { get; set; }

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

    /// <summary>
    /// Raised whenever this tab's Source is torn down and rebuilt (RebuildSource - every
    /// navigation, and view-mode toggling) or this tab is otherwise handed real activation
    /// (MainPanelViewModel.RequestFocus, from SwitchPanel/a mouse click activating the panel).
    /// None of those move actual Avalonia keyboard focus on their own - the View (ItemBrowser)
    /// subscribes to this to put focus back in the grid, since without it focus is left to
    /// whatever FocusManager's own fallback search lands on once the previously-focused element
    /// is torn down (observed: the toolbar's first button), not wherever the user is working.
    /// </summary>
    public event Action? FocusRequested;

    public void RequestFocus() => FocusRequested?.Invoke();

    /// <summary>
    /// Raised with a row index whenever the current item changes programmatically (GotoFirstItem/
    /// GotoLastItem/GoBackToParentFolder's restore, the new-listing default - see SetCurrentRow),
    /// as opposed to via TreeDataGrid's own keyboard/pointer handling. Only *that* internal
    /// handling scrolls the row into view on its own (see
    /// TreeDataGridRowSelectionModel.MoveSelection's RowsPresenter.BringIntoView call) - setting
    /// SelectedIndex directly, which is all any of the above do, never does. The View subscribes
    /// to this to call the same BringIntoView itself, since only it has a reference to FileGrid.
    /// </summary>
    public event Action<int>? ScrollToRowRequested;

    // The one place the current item is ever set to a specific row index - keeps selection and
    // "make sure it's actually visible" (ScrollToRowRequested above) together.
    //
    // Setting SelectedIndex, not calling Select(): the selection model is multi-select
    // (SingleSelect = false, for future multi-file operations), and Select() only *adds* to
    // whatever's already selected - once anything is selected, a later Select() call doesn't move
    // SelectedItem/SelectedIndex at all, it just grows the selection. SelectedIndex's setter
    // (Clear() + Select()) is the one that actually replaces the current item, which matters the
    // moment more than one caller sets the current row during the same navigation (e.g.
    // GoBackToParentFolder's default-then-restore).
    private void SetCurrentRow(int rowIndex)
    {
        if (SelectionModel is { } selectionModel)
            selectionModel.SelectedIndex = new IndexPath(rowIndex);

        ScrollToRowRequested?.Invoke(rowIndex);
    }

    public ICommand NavigateToHistoryEntryCommand { get; }

    public ItemBrowserViewModel(FileSystemProviderRegistry providers, IconCache iconCache)
    {
        _providers = providers;
        _iconCache = iconCache;

        ToggleViewModeCommand = ReactiveCommand.Create(ToggleViewMode);
        NavigateToHistoryEntryCommand = ReactiveCommand.Create<string>(path => _ = NavigateToAsync(path));

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.GoIntoCurrentFolder] = ReactiveCommand.Create(GoIntoCurrentFolder),
            [Operation.GoBackToParentFolder] = ReactiveCommand.Create(GoBackToParentFolder),
            [Operation.GotoFirstItem] = ReactiveCommand.Create(GotoFirstItem),
            [Operation.GotoLastItem] = ReactiveCommand.Create(GotoLastItem),
            [Operation.ReverseSelection] = ReactiveCommand.Create(ReverseSelection),
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

            if (provider.TracksHistory)
                RecordHistory(path);

            RebuildSource();
            RecomputeTotals();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to navigate to {0}", path);
        }
    }

    private void RecordHistory(string path)
    {
        NavigationHistory.Remove(path);
        NavigationHistory.Insert(0, path);

        while (NavigationHistory.Count > MaxHistoryEntries)
            NavigationHistory.RemoveAt(NavigationHistory.Count - 1);
    }

    private static IReadOnlyList<IFileSystemItem> Sort(IReadOnlyList<IFileSystemItem> items) =>
        items
            .OrderByDescending(i => i.ItemType == FileSystemItemType.Directory)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void RebuildSource()
    {
        (Source as IDisposable)?.Dispose();
        FilterText = string.Empty;
        IsFilterActive = false;
        _rows = BuildRows(_allItems);

        if (ViewMode == ItemBrowserViewMode.List)
            Source = BuildFlatSource(_rows);
        else
            Source = BuildHierarchicalSource(_rows);

        // A fresh listing defaults its current item to the first row rather than leaving nothing
        // selected, matching Total Commander. GoBackToParentFolder's NavigateBackToParentAsync
        // overrides this with a more specific choice (the folder just left) right after this
        // returns; every other navigation path keeps this default.
        if (Source.Rows.Count > 0)
            SetCurrentRow(0);

        RequestFocus();
    }

    private FlatTreeDataGridSource<FileItemRow> BuildFlatSource(IReadOnlyList<FileItemRow> rows)
    {
        var source = new FlatTreeDataGridSource<FileItemRow>(rows)
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
        return source;
    }

    private HierarchicalTreeDataGridSource<FileItemRow> BuildHierarchicalSource(IReadOnlyList<FileItemRow> rows)
    {
        var nameColumn = new HierarchicalExpanderColumn<FileItemRow>(
            CreateNameColumn(),
            ChildSelector,
            hasChildrenSelector: x => x.Item.ItemType == FileSystemItemType.Directory);

        var source = new HierarchicalTreeDataGridSource<FileItemRow>(rows)
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
        return source;
    }

    /// <summary>
    /// Recomputes which of _rows currently match FilterText (see QuickFilter) and pushes just
    /// the matches into Source.Items - swapping Items on the existing source rather than
    /// RebuildSource's tear-down-and-recreate approach, since that would also reconstruct every
    /// FileItemRow and re-trigger its async icon load for what needs to be an instant
    /// per-keystroke update.
    ///
    /// Swapping Items resets the selection model's source (TreeDataGridRowSelectionModel drops
    /// any current-row cursor no longer present in the new set) - that's cursor-only, though;
    /// a row newly filtered out is also explicitly unmarked here, since IsMarked is tracked
    /// independently of the grid's selection model and swapping Items alone wouldn't touch it -
    /// this is what actually keeps "marked" a subset of "visible" (see FileItemRow.IsMarked).
    /// </summary>
    private void ApplyFilter()
    {
        foreach (var row in _rows)
        {
            row.IsVisible = QuickFilter.Matches(FilterText, row.Item.Name);
            if (!row.IsVisible)
                row.IsMarked = false;
        }

        if (Source is null)
            return;

        Source.Items = _rows.Where(r => r.IsVisible).ToList();

        if (Source.Rows.Count > 0)
            SetCurrentRow(0);

        RecomputeSelection();

        // Swapping Items recycles TreeDataGrid's row/cell containers, which can leave real
        // keyboard focus sitting on a now-disposed cell instead of following the still-current
        // row - re-request it defensively so a fast burst of filter keystrokes never drops out of
        // the grid mid-typing.
        RequestFocus();
    }

    private void SetFilterText(string text)
    {
        FilterText = text;
        IsFilterActive = FilterText.Length > 0;
        ApplyFilter();
    }

    /// <summary>
    /// Appends typed characters to the quick filter - called by the View when printable text is
    /// typed while FileGrid has focus, intercepted before TreeDataGrid's own built-in
    /// type-ahead-jump TextInput handling can see it (see ItemBrowser.axaml.cs).
    /// </summary>
    public void AppendFilterText(string text) => SetFilterText(FilterText + text);

    /// <summary>
    /// Backspace while the filter is active - called by the View instead of letting Backspace
    /// fall through to (no-op) default grid handling.
    /// </summary>
    public void RemoveLastFilterCharacter()
    {
        if (FilterText.Length == 0)
            return;

        SetFilterText(FilterText[..^1]);
    }

    /// <summary>
    /// Escape while the filter is active - called by the View to close the filter bar and show
    /// the folder's full contents again.
    /// </summary>
    public void ClearFilter()
    {
        if (FilterText.Length == 0)
            return;

        SetFilterText(string.Empty);
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
            return BuildRows(Sort(children));
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to expand {0}", row.Item.FullPath);
            return null;
        }
    }

    private List<FileItemRow> BuildRows(IReadOnlyList<IFileSystemItem> items) =>
        items.Select(i => new FileItemRow(i, _iconCache)).ToList();

    // Marked-row coloring (background and text) is handled entirely in ItemBrowser.axaml via
    // style selectors bound to FileItemRow.IsMarked, not here - TreeDataGridRow's DataContext is
    // the FileItemRow model, and covers every column uniformly (not just this one), unlike a
    // per-column Foreground binding would.
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

    private void RecomputeTotals()
    {
        TotalFileCount = _allItems.Count(i => i.ItemType == FileSystemItemType.File);
        TotalFolderCount = _allItems.Count(i => i.ItemType == FileSystemItemType.Directory);
        TotalSize = _allItems.Where(i => i.ItemType == FileSystemItemType.File).Sum(i => i.Size);
        RecomputeSelection();
    }

    // Driven by IsMarked (Space), not the grid's own SelectionModel - the cursor moving around
    // the grid no longer changes what's counted here, only marking/unmarking does.
    private void RecomputeSelection()
    {
        var marked = _rows.Where(r => r.IsMarked).Select(r => r.Item).ToList();

        SelectedFileCount = marked.Count(i => i.ItemType == FileSystemItemType.File);
        SelectedFolderCount = marked.Count(i => i.ItemType == FileSystemItemType.Directory);
        SelectedSize = marked.Where(i => i.ItemType == FileSystemItemType.File).Sum(i => i.Size);
    }

    /// <summary>
    /// Space - toggles the marked state of whatever row is currently under the cursor (see
    /// FileItemRow.IsMarked). Called by the View only when the quick filter isn't active; while
    /// typing a filter, Space is a word separator instead (ItemBrowser.axaml.cs's
    /// OnPreviewTextInput picks between the two).
    /// </summary>
    public void ToggleMarkCurrentItem()
    {
        if (SelectionModel?.SelectedItem is not { } current)
            return;

        current.IsMarked = !current.IsMarked;
        RecomputeSelection();
    }

    // Alt+R - flips every currently visible row's marked state. Scoped to IsVisible rows only
    // (not all of _rows): rows hidden by the quick filter are never marked (see ApplyFilter), and
    // reversing them too would mark rows the user can't currently see, breaking that invariant.
    private void ReverseSelection()
    {
        foreach (var row in _rows.Where(r => r.IsVisible))
            row.IsMarked = !row.IsMarked;

        RecomputeSelection();
    }

    /// <summary>
    /// What Copy/Move/Delete operate on: every marked row if any are marked, otherwise whatever's
    /// under the cursor right now - Total Commander's own fallback, so a single-item operation
    /// never requires marking first.
    /// </summary>
    public IReadOnlyList<IFileSystemItem> GetOperationTargets()
    {
        var marked = _rows.Where(r => r.IsMarked).Select(r => r.Item).ToList();
        if (marked.Count > 0)
            return marked;

        return SelectionModel?.SelectedItem is { } current
            ? new[] { current.Item }
            : Array.Empty<IFileSystemItem>();
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
        var path = GetSelectedEnterablePath();
        if (path is not null)
            _ = NavigateToAsync(path);
    }

    /// <summary>
    /// The currently-selected item's path, if it's a single directory the active provider can
    /// enter - otherwise null. Used by MainPanelViewModel's OpenSelectedFolderInNewTab, which
    /// needs this same "what folder is selected right now" check but, unlike GoIntoCurrentFolder,
    /// acts on a sibling tab rather than this one.
    /// </summary>
    public string? GetSelectedEnterablePath()
    {
        var selected = SelectionModel?.SelectedItem;
        return selected is not null && _provider is not null && _provider.CanEnter(selected.Item)
            ? selected.Item.FullPath
            : null;
    }

    private void GoBackToParentFolder()
    {
        var childPath = CurrentPath.TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(childPath);
        if (!string.IsNullOrEmpty(parent))
            _ = NavigateBackToParentAsync(parent, childPath);
    }

    // Keeps the folder just left selected in the parent listing, so browsing down a tree of
    // subfolders one at a time is Right, Left, Down, Right, Left, Down, ... instead of Left
    // dropping the cursor back to the top of the list every time.
    private async Task NavigateBackToParentAsync(string parent, string childPath)
    {
        await NavigateToAsync(parent);
        SelectItemByPath(childPath);
    }

    private void SelectItemByPath(string path)
    {
        var itemIndex = -1;
        for (var i = 0; i < _allItems.Count; i++)
        {
            if (string.Equals(_allItems[i].FullPath, path, StringComparison.OrdinalIgnoreCase))
            {
                itemIndex = i;
                break;
            }
        }

        if (itemIndex < 0)
            return;

        SetCurrentRow(itemIndex);
    }

    private void GotoFirstItem()
    {
        if (Source?.Rows.Count > 0)
            SetCurrentRow(0);
    }

    private void GotoLastItem()
    {
        var count = Source?.Rows.Count ?? 0;
        if (count > 0)
            SetCurrentRow(count - 1);
    }

    public ICommand? GetCommand(Operation operation) => _commands.GetValueOrDefault(operation);
}
