using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using CatCommander.Models;
using CatCommander.Utils;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for MainPanel - represents one file browser pane
/// </summary>
public class MainPanelViewModel : INotifyPropertyChanged
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private string _currentPath = string.Empty;
    private FileItemTreeNode? _fileTree;
    private ObservableCollection<IFileSystemItem> _items = new();
    private IFileSystemItem? _selectedItem;
    private bool _isActive;

    public MainPanelViewModel()
    {
        log.Info("MainPanelViewModel initialized");

        // Initialize with home directory
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        NavigateToPath(homePath);

        // Initialize commands
        RefreshCommand = ReactiveCommand.Create(ExecuteRefresh, outputScheduler: RxApp.MainThreadScheduler);
        NavigateUpCommand = ReactiveCommand.Create(ExecuteNavigateUp, CanExecuteNavigateUpObservable, RxApp.MainThreadScheduler);
        NavigateToCommand = ReactiveCommand.Create<string>(ExecuteNavigateTo, outputScheduler: RxApp.MainThreadScheduler);
    }

    #region Properties

    /// <summary>
    /// Current directory path
    /// </summary>
    public string CurrentPath
    {
        get => _currentPath;
        private set
        {
            if (_currentPath != value)
            {
                _currentPath = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// File tree for the current directory (for archives/virtual filesystems)
    /// </summary>
    public FileItemTreeNode? FileTree
    {
        get => _fileTree;
        private set
        {
            _fileTree = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Items to display in the file browser (current directory contents)
    /// </summary>
    public ObservableCollection<IFileSystemItem> Items
    {
        get => _items;
        private set
        {
            _items = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Currently selected item
    /// </summary>
    public IFileSystemItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether this panel is the active/focused panel
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Whether current path is at root (cannot navigate up)
    /// </summary>
    public bool IsAtRoot
    {
        get
        {
            if (string.IsNullOrEmpty(CurrentPath))
                return true;

            try
            {
                var dirInfo = new DirectoryInfo(CurrentPath);
                return dirInfo.Parent == null;
            }
            catch
            {
                return true;
            }
        }
    }

    #endregion

    #region Commands

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateUpCommand { get; }
    public ReactiveCommand<string, Unit> NavigateToCommand { get; }

    #endregion

    #region Navigation Methods

    /// <summary>
    /// Navigate to a specific path
    /// </summary>
    public void NavigateToPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            log.Warn("Attempted to navigate to empty path");
            return;
        }

        try
        {
            // Check if path exists
            if (!Directory.Exists(path))
            {
                log.Warn($"Path does not exist: {path}");
                return;
            }

            CurrentPath = path;
            LoadDirectory(path);

            log.Debug($"Navigated to: {path}");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to navigate to path: {path}");
        }
    }

    /// <summary>
    /// Navigate into a subdirectory or open a file
    /// </summary>
    public void NavigateInto(IFileSystemItem item)
    {
        if (item == null)
            return;

        if (item.ItemType == FileSystemItemType.Directory)
        {
            NavigateToPath(item.FullPath);
        }
        else if (item.ItemType == FileSystemItemType.Special && item.Name == "..")
        {
            NavigateUp();
        }
        else
        {
            // TODO: Open file with associated application
            log.Info($"Open file: {item.FullPath}");
        }
    }

    /// <summary>
    /// Navigate to parent directory
    /// </summary>
    public void NavigateUp()
    {
        if (IsAtRoot)
            return;

        try
        {
            var dirInfo = new DirectoryInfo(CurrentPath);
            if (dirInfo.Parent != null)
            {
                NavigateToPath(dirInfo.Parent.FullName);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to navigate up");
        }
    }

    /// <summary>
    /// Refresh the current directory
    /// </summary>
    public void Refresh()
    {
        if (!string.IsNullOrEmpty(CurrentPath))
        {
            LoadDirectory(CurrentPath);
            log.Debug($"Refreshed: {CurrentPath}");
        }
    }

    #endregion

    #region Private Methods

    private void LoadDirectory(string path)
    {
        try
        {
            var items = new List<IFileSystemItem>();

            // Add parent directory entry if not at root
            var dirInfo = new DirectoryInfo(path);
            if (dirInfo.Parent != null)
            {
                items.Add(new FileItemModel
                {
                    Name = "..",
                    FullPath = dirInfo.Parent.FullName,
                    ItemType = FileSystemItemType.Special,
                    Modified = DateTime.MinValue,
                    Extension = string.Empty
                });
            }

            // Add directories
            foreach (var dir in Directory.GetDirectories(path))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    items.Add(new FileItemModel
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        ItemType = FileSystemItemType.Directory,
                        Created = info.CreationTime,
                        Modified = info.LastWriteTime,
                        Accessed = info.LastAccessTime,
                        Extension = string.Empty,
                        IsHidden = (info.Attributes & FileAttributes.Hidden) != 0
                    });
                }
                catch (Exception ex)
                {
                    log.Error(ex, $"Failed to access directory: {dir}");
                }
            }

            // Add files
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    var info = new FileInfo(file);
                    items.Add(new FileItemModel
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        ItemType = FileSystemItemType.File,
                        Size = info.Length,
                        Created = info.CreationTime,
                        Modified = info.LastWriteTime,
                        Accessed = info.LastAccessTime,
                        Extension = info.Extension,
                        IsHidden = (info.Attributes & FileAttributes.Hidden) != 0
                    });
                }
                catch (Exception ex)
                {
                    log.Error(ex, $"Failed to access file: {file}");
                }
            }

            // Update the items collection
            Items = new ObservableCollection<IFileSystemItem>(items);

            // Create a file tree from the items (useful for future archive browsing)
            if (items.Any())
            {
                FileTree = FileItemTreeNode.CreateFrom(items);
            }
            else
            {
                FileTree = null;
            }

            log.Debug($"Loaded {items.Count} items from {path}");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Error loading directory: {path}");
            Items = new ObservableCollection<IFileSystemItem>();
            FileTree = null;
        }
    }

    #endregion

    #region Command Implementations

    private IObservable<bool> CanExecuteNavigateUpObservable =>
        this.WhenAnyValue(x => x.CurrentPath)
            .Select(_ => !IsAtRoot);

    private void ExecuteRefresh()
    {
        log.Info("Refresh command executed");
        Refresh();
    }

    private void ExecuteNavigateUp()
    {
        log.Info("NavigateUp command executed");
        NavigateUp();
    }

    private void ExecuteNavigateTo(string path)
    {
        log.Info($"NavigateTo command executed: {path}");
        NavigateToPath(path);
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}
