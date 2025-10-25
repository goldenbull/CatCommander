using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using CatCommander.Models;
using NLog;

namespace CatCommander.ViewModels;

public class ItemsBrowserViewModel : INotifyPropertyChanged
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private string _currentPath = string.Empty;
    private HierarchicalTreeDataGridSource<FileItemModel>? _fileItems;
    private int _selectedCount;

    public ItemsBrowserViewModel()
    {
        log.Info("Initializing ItemsBrowserViewModel");

        // Initialize with the user's home directory
        InitializeFileItems();
        PathHistory = new ObservableCollection<string>();
        CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string CurrentPath
    {
        get => _currentPath;
        set
        {
            if (_currentPath != value)
            {
                _currentPath = value;
                OnPropertyChanged();
                LoadDirectory(value);

                // Add to path history if not already present
                if (!PathHistory.Contains(value))
                {
                    PathHistory.Insert(0, value);
                    if (PathHistory.Count > 20) // Keep only last 20 paths
                    {
                        PathHistory.RemoveAt(PathHistory.Count - 1);
                    }
                }
            }
        }
    }

    public ObservableCollection<string> PathHistory { get; }

    public HierarchicalTreeDataGridSource<FileItemModel>? FileItems
    {
        get => _fileItems;
        private set
        {
            _fileItems = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Number of selected items
    /// </summary>
    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (_selectedCount != value)
            {
                _selectedCount = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the currently selected items
    /// </summary>
    public IEnumerable<FileItemModel> SelectedItems
    {
        get
        {
            if (FileItems?.Selection is TreeDataGridRowSelectionModel<FileItemModel> selection)
            {
                return selection.SelectedItems.Where(item => item != null)!;
            }
            return Enumerable.Empty<FileItemModel>();
        }
    }

    private void InitializeFileItems()
    {
        var source = new HierarchicalTreeDataGridSource<FileItemModel>(Array.Empty<FileItemModel>())
        {
            Columns =
            {
                new HierarchicalExpanderColumn<FileItemModel>(
                    new TextColumn<FileItemModel, string>("Name", x => x.Name, GridLength.Star),
                    x => null), // No children for now (flat list)
                new TextColumn<FileItemModel, string>("Extension", x => x.Extension, new GridLength(100)),
                new TextColumn<FileItemModel, string>("Size", x => x.DisplaySize, new GridLength(100)),
                new TextColumn<FileItemModel, DateTime>("Modified", x => x.Modified, new GridLength(150))
            }
        };

        ConfigureSelection(source);
        FileItems = source;
    }

    private void ConfigureSelection(HierarchicalTreeDataGridSource<FileItemModel> source)
    {
        // RowSelection is auto-initialized, just configure it
        if (source.RowSelection != null)
        {
            source.RowSelection.SingleSelect = false;
            source.RowSelection.SelectionChanged += OnSelectionChanged;
        }
    }

    private void OnSelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<FileItemModel> e)
    {
        SelectedCount = SelectedItems.Count();
        log.Debug($"Selection changed: {SelectedCount} items selected");
    }

    /// <summary>
    /// Toggles the selection of the currently focused item (Space key handler)
    /// </summary>
    public void ToggleCurrentItemSelection()
    {
        if (FileItems?.RowSelection is TreeDataGridRowSelectionModel<FileItemModel> selection)
        {
            // Get the current focused row index
            var focusedIndex = selection.AnchorIndex;
            if (focusedIndex.Count > 0)
            {
                var rowIndex = focusedIndex[0];

                // Get the item at that index
                var items = FileItems.Items.ToList();
                if (rowIndex >= 0 && rowIndex < items.Count)
                {
                    var item = items[rowIndex];

                    // Toggle selection
                    if (selection.IsSelected(focusedIndex))
                    {
                        selection.Deselect(focusedIndex);
                        log.Debug($"Deselected item: {item?.Name}");
                    }
                    else
                    {
                        selection.Select(focusedIndex);
                        log.Debug($"Selected item: {item?.Name}");
                    }
                }
            }
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
            var items = new ObservableCollection<FileItemModel>();

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

            // Update the TreeDataGrid source
            var source = new HierarchicalTreeDataGridSource<FileItemModel>(items)
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<FileItemModel>(
                        new TextColumn<FileItemModel, string>("Name", x => x.Name, GridLength.Star),
                        x => null),
                    new TextColumn<FileItemModel, string>("Extension", x => x.Extension, new GridLength(100)),
                    new TextColumn<FileItemModel, string>("Size", x => x.DisplaySize, new GridLength(100)),
                    new TextColumn<FileItemModel, DateTime>("Modified", x => x.Modified, new GridLength(150))
                }
            };

            ConfigureSelection(source);
            FileItems = source;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error loading directory: {path}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}