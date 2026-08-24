using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using CatCommander.Config;
using CatCommander.Models;
using DynamicData;
using Metalama.Patterns.Observability;
using NLog;

namespace CatCommander.ViewModels;

/// <summary>
/// represents all data shown in the panel.
/// most important properties:
///     1. RootPath. Could be:
///         - an ordinary folder
///         - fullpath of a zip file
///         - String.Empty for special cases, like the search result, or flatten file list of folders
///     2. AllItems. Could be:
///         - normal filesystem
///         - zip entries, which are organized into trees
///         - an arbitrary list of file items for some special cases
///     3. FilterText and VisibleItems. There are two kinds of filter:
///         - smart mode, apply all words in filter to the item list one by one
///         - regex mode
///     4. SelectedItems. Always select the visible items, invisible items are always un-selected.
/// </summary>
[Observable]
public partial class ItemBrowserViewModel
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public ItemBrowserViewModel()
    {
        log.Info("Initializing ItemsBrowserViewModel");
        VisibleItemsSource = new(VisibleItems)
        {
            Columns =
            {
                new TextColumn<FileItemModel, string>("Name", x => x.Name, GridLength.Star),
                new TextColumn<FileItemModel, string>("Extension", x => x.Extension, new GridLength(100)),
                new TextColumn<FileItemModel, string>("Size", x => x.DisplaySize, new GridLength(100)),
                new TextColumn<FileItemModel, DateTime>("Modified", x => x.Modified, new GridLength(150))
            }
        };

        VisibleItemsSource.RowSelection!.SingleSelect = false;
        VisibleItemsSource.RowSelection!.SelectionChanged += OnSelectionChanged;
    }

    // point to global data
    public ObservableCollection<string> PathHistory => ConfigManager.Instance.Application.PathHistory;

    #region RootPath

    private string _rootPath = string.Empty;

    public string RootPath
    {
        get => _rootPath;
        set
        {
            if (_rootPath == value)
                return;

            _rootPath = value;
            Title = Path.GetFileName(value);
            LoadDirectory(value);

            // Add to path history if not already present, or move to top if already exists
            PathHistory.Remove(value);
            PathHistory.Insert(0, value);
            if (PathHistory.Count > 30) // Keep only last several paths
            {
                PathHistory.RemoveAt(PathHistory.Count - 1);
            }

            OnPropertyChanged(nameof(RootPath));
        }
    }

    public bool CanNavigateUp
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_rootPath))
                return false;
            return !Path.IsPathRooted(_rootPath);
        }
    }

    private void LoadDirectory(string path)
    {
        log.Debug($"LoadDirectory called with path: {path}");

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            log.Warn($"Invalid or non-existent path: {path}");
            return;
        }

        try
        {
            var items = new List<FileItemModel>();

            // Add parent directory entry if not at root
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Parent != null)
            {
                items.Add(new FileItemModel
                {
                    Name = "..",
                    FullPath = dirInfo.Parent.FullName,
                    IsDirectory = true,
                    Modified = DateTime.MinValue,
                    Extension = string.Empty
                });
            }

            // Add directories
            int dirCount = 0;
            foreach (var dir in Directory.GetDirectories(path))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    items.Add(new FileItemModel
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = true,
                        Modified = info.LastWriteTime,
                        Extension = string.Empty
                    });
                    dirCount++;
                }
                catch (Exception ex)
                {
                    log.Error(ex, $"Failed to access directory: {dir}");
                }
            }

            // Add files
            int fileCount = 0;
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    var info = new FileInfo(file);
                    items.Add(new FileItemModel
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        IsDirectory = false,
                        Size = info.Length,
                        Modified = info.LastWriteTime,
                        Extension = info.Extension
                    });
                    fileCount++;
                }
                catch (Exception ex)
                {
                    log.Error(ex, $"Failed to access file: {file}");
                }
            }

            log.Debug($"Loaded directory {path}: {dirCount} directories, {fileCount} files");

            // Store all items for filtering
            allItems.Clear();
            allItems.AddRange(items);

            // Apply current filter (if any) and update the TreeDataGrid source
            ApplyFilter();
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error loading directory: {path}");
        }
    }

    #endregion

    #region all items and filter

    private readonly List<FileItemModel> allItems = new();
    private readonly ObservableCollection<FileItemModel> VisibleItems = new();
    public FlatTreeDataGridSource<FileItemModel> VisibleItemsSource { get; }

    // Filter text for filtering file items
    private string _filterText = string.Empty;

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (_filterText != value)
            {
                _filterText = value;
                ApplyFilter();
            }
        }
    }

    /// <summary>
    /// Applies the current filter to the file items
    /// </summary>
    private void ApplyFilter()
    {
        if (allItems.Count == 0)
        {
            return;
        }

        VisibleItems.Clear();
        if (string.IsNullOrWhiteSpace(_filterText))
        {
            // No filter - show all items
            VisibleItems.AddRange(allItems);
        }
        else
        {
            // Apply filter - case-insensitive search in file name
            var filterLower = _filterText.ToLowerInvariant();

            // smart mode
            var filterWords = filterLower.Split();
            foreach (var item in allItems)
            {
                var name = item.Name.ToLowerInvariant();
                if (filterWords.All(w => name.Contains(w)))
                {
                    VisibleItems.Add(item);
                }
            }

            log.Debug($"Filtered {allItems.Count} items to {VisibleItems.Count} using filter: {_filterText}");
        }

        // TODO update selection
        // ConfigureSelection(source);
    }

    #endregion

    #region selection

    [NotObservable]
    private List<FileItemModel> SelectedItems
    {
        get
        {
            if (VisibleItemsSource.Selection is TreeDataGridRowSelectionModel<FileItemModel> selection)
                return selection.SelectedItems.Where(item => item != null).ToList()!;
            return [];
        }
    }

    public int SelectedCount { get; private set; }

    private void OnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileItemModel> e)
    {
        SelectedCount = SelectedItems.Count;
        log.Debug($"Selection changed: {SelectedCount} items selected");
    }

    #endregion

    #region other properties

    public string StatusText { get; private set; } = "selected 123/999 bytes, 2/5 files, 4/20 folders";
    public string Title { get; private set; } = string.Empty;

    #endregion

    public void Refresh()
    {
    }

    public void NavigateUp()
    {
    }

    public void NavigateToPath(string path)
    {
    }
}