using System.Collections.ObjectModel;
using CatCommander.Commands;
using CatCommander.Models;
using CatCommander.Utils;
using Metalama.Patterns.Observability;
using NLog;

namespace CatCommander.ViewModels;

[Observable]
public partial class MainWindowViewModel
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public MainWindowViewModel()
    {
        // Initialize the two panels (left and right)
        LeftPanel = new MainPanelViewModel();
        RightPanel = new MainPanelViewModel();
        FileSystemHelper.UpdateDeviceList();
        
        // Set the reference to this view model in CommandExecutor
        CommandExecutor.Instance.MainWindowViewModel = this;
        CommandExecutor.Instance.ActivePanel = LeftPanel;

        log.Info("MainWindowViewModel initialized");
    }

    #region Panel ViewModels

    public MainPanelViewModel LeftPanel { get; }
    public MainPanelViewModel RightPanel { get; }
    public ObservableCollection<DriveInfoModel> DeviceList => FileSystemHelper.DeviceList;
    
    #endregion

    /// <summary>
    /// Provides access to all application commands through the CommandExecutor singleton
    /// </summary>
    public CommandExecutor CmdExecutor => CommandExecutor.Instance;
}