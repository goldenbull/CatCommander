using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CatCommander.Commands;
using CatCommander.Models;
using CatCommander.Utils;
using Metalama.Patterns.Observability;
using NLog;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for MainPanel - represents one file browser pane
/// </summary>
[Observable]
public partial class MainPanelViewModel
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public MainPanelViewModel()
    {
        log.Info("MainPanelViewModel initialized");

        // Initialize the collection of browser view models (for future tabs support)
        ItemsBrowsers = new ObservableCollection<ItemsBrowserViewModel>();

        // Add the first browser instance (default tab)
        var initialBrowser = new ItemsBrowserViewModel();
        ItemsBrowsers.Add(initialBrowser);
        ActiveBrowser = initialBrowser;

        // Initialize with home directory
        var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        NavigateToPath(homePath);
    }

    #region Properties

    /// <summary>
    /// Collection of ItemsBrowser view models (for tabs support)
    /// </summary>
    public ObservableCollection<ItemsBrowserViewModel> ItemsBrowsers { get; }

    /// <summary>
    /// Currently active browser tab
    /// </summary>
    public ItemsBrowserViewModel? ActiveBrowser { get; set; }

    /// <summary>
    /// Current directory path (delegates to active browser)
    /// </summary>
    public string CurrentPath
    {
        get => ActiveBrowser?.CurrentPath ?? string.Empty;
        private set
        {
            if (ActiveBrowser != null)
            {
                ActiveBrowser.CurrentPath = value;
            }
        }
    }

    /// <summary>
    /// File tree for the current directory (for archives/virtual filesystems)
    /// </summary>
    public FileItemTreeNode? FileTree { get; private set; }

    /// <summary>
    /// Items to display in the file browser (current directory contents)
    /// </summary>
    public ObservableCollection<IFileSystemItem> Items { get; private set; } = new();

    /// <summary>
    /// Currently selected item
    /// </summary>
    public IFileSystemItem? SelectedItem { get; set; }

    /// <summary>
    /// Whether this panel is the active/focused panel
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Whether current path is at root (cannot navigate up)
    /// </summary>
    [NotObservable]
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

    /// <summary>
    /// Provides access to all application commands through the CommandExecutor singleton
    /// </summary>
    public CommandExecutor CmdExecutor => CommandExecutor.Instance;

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
}
