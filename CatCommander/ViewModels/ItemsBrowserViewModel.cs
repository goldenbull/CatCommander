using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using CatCommander.Models;
using CatCommander.Services;
using Microsoft.Extensions.Logging;

namespace CatCommander.ViewModels;

public class ItemsBrowserViewModel : INotifyPropertyChanged
{
    private readonly ILogger<ItemsBrowserViewModel> _logger;
    private string _currentPath = string.Empty;
    private HierarchicalTreeDataGridSource<FileItemModel>? _fileItems;

    public ItemsBrowserViewModel()
    {
        _logger = LoggingService.CreateLogger<ItemsBrowserViewModel>();
        _logger.LogInformation("Initializing ItemsBrowserViewModel");

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

    private void InitializeFileItems()
    {
        FileItems = new HierarchicalTreeDataGridSource<FileItemModel>(Array.Empty<FileItemModel>())
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
    }

    private void LoadDirectory(string path)
    {
        _logger.LogDebug("LoadDirectory called with path: {Path}", path);

        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            _logger.LogWarning("Invalid or non-existent path: {Path}", path);
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
                    _logger.LogWarning(ex, "Failed to access directory: {Directory}", dir);
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
                    _logger.LogWarning(ex, "Failed to access file: {File}", file);
                }
            }

            _logger.LogInformation("Loaded directory {Path}: {DirCount} directories, {FileCount} files",
                path, dirCount, fileCount);

            // Update the TreeDataGrid source
            FileItems = new HierarchicalTreeDataGridSource<FileItemModel>(items)
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading directory: {Path}", path);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
