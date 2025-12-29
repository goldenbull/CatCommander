using System;
using System.Collections.ObjectModel;
using System.IO;
using CatCommander.Commands;
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
        var initialBrowser = new ItemBrowserViewModel();
        // Initialize with home directory. TODO: load from saved history
        initialBrowser.CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        RootFileItems.Add(initialBrowser);
        ActiveFileItem = initialBrowser;
    }

    #region Properties

    public ObservableCollection<string> DeviceList { get; } = new();

    /// <summary>
    /// Collection of ItemsBrowser view models (for tabs support)
    /// </summary>
    public ObservableCollection<ItemBrowserViewModel> RootFileItems { get; } = new();

    public bool IsActive { get; set; }

    /// <summary>
    /// Currently active browser tab
    /// </summary>
    public ItemBrowserViewModel? ActiveFileItem { get; set; }

    #endregion
}