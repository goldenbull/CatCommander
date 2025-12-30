using System;
using System.Collections.ObjectModel;
using CatCommander.Config;
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

        // Add the first browser instance (default tab)
        var vmTabItem = new ItemBrowserViewModel();
        RootFileItems.Add(vmTabItem);
        // Initialize with home directory. TODO: load from saved history
        vmTabItem.CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        ActiveFileItem = vmTabItem;

        // add second tab for test
        vmTabItem = new ItemBrowserViewModel();
        RootFileItems.Add(vmTabItem);
        vmTabItem.CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    #region Properties

    public ApplicationSettings Settings => ConfigManager.Instance.Application;

    public bool IsActive { get; set; }
    
    /// <summary>
    /// Collection of ItemsBrowser view models (for tabs support)
    /// </summary>
    public ObservableCollection<ItemBrowserViewModel> RootFileItems { get; } = new();

    /// <summary>
    /// Currently active browser tab
    /// </summary>
    public ItemBrowserViewModel? ActiveFileItem { get; set; }

    #endregion
}