using CatCommander.Commands;
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

        // Set left panel as active by default
        LeftPanel.IsActive = true;

        // Set the reference to this view model in CommandExecutor
        CommandExecutor.Instance.MainWindowViewModel = this;

        log.Info("MainWindowViewModel initialized");
    }

    #region Panel ViewModels

    public MainPanelViewModel LeftPanel { get; }
    public MainPanelViewModel RightPanel { get; }

    #endregion

    /// <summary>
    /// Provides access to all application commands through the CommandExecutor singleton
    /// </summary>
    public CommandExecutor CmdExecutor => CommandExecutor.Instance;
}