using System;
using System.Collections.Generic;
using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.View;
using Metalama.Patterns.Observability;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

[Observable]
public partial class MainWindowViewModel : IShortcutCommandSource
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly ConfigManager _configManager;
    private readonly Func<FindWindow> _findWindowFactory;
    private readonly Func<BatchRenameWindow> _batchRenameWindowFactory;
    private readonly Dictionary<Operation, ICommand> _commands;

    public MainPanelViewModel LeftPanel { get; }
    public MainPanelViewModel RightPanel { get; }

    /// <summary>
    /// The panel that panel-scoped operations (Copy/Move/... once implemented) act on. Updated
    /// from real focus changes (mouse click into a panel) as well as SwitchPanel (Tab) - see
    /// MainPanelViewModel.OnActivated - not just from the Tab shortcut, unlike the old
    /// implementation this replaces.
    /// </summary>
    public MainPanelViewModel? ActivePanel { get; private set; }

    // Named properties, one per Operation this window answers, for XAML Command bindings
    // (menu items, toolbar buttons). Each is also registered in _commands under its Operation so
    // ShortcutRouter/GlobalShortcutGuard dispatch to the exact same command instance - there's
    // only ever one ICommand per Operation, just exposed two ways.
    public ICommand CopyCommand { get; }
    public ICommand MoveCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand OpenFindCommand { get; }
    public ICommand OpenBatchRenameCommand { get; }

    // Settings actions - not Operations (no keyboard-configurable gesture, no TC precedent), so
    // these are plain named commands only, not registered in _commands/GetCommand.
    public ICommand RestoreDefaultShortcutsCommand { get; }
    public ICommand SetWindowsKeyboardStyleCommand { get; }
    public ICommand SetMacKeyboardStyleCommand { get; }

    public MainWindowViewModel(
        ConfigManager configManager,
        Func<MainPanelViewModel> mainPanelFactory,
        Func<FindWindow> findWindowFactory,
        Func<BatchRenameWindow> batchRenameWindowFactory)
    {
        _configManager = configManager;

        // Two distinct instances of the same type - needs a factory, not direct constructor
        // injection, same reasoning as MainPanelViewModel needing one for ItemBrowserViewModel.
        LeftPanel = mainPanelFactory();
        RightPanel = mainPanelFactory();
        _findWindowFactory = findWindowFactory;
        _batchRenameWindowFactory = batchRenameWindowFactory;

        LeftPanel.OnActivated = () => SetActivePanel(LeftPanel);
        RightPanel.OnActivated = () => SetActivePanel(RightPanel);
        SetActivePanel(LeftPanel);

        CopyCommand = ReactiveCommand.Create(() => LogStubOperation(Operation.Copy));
        MoveCommand = ReactiveCommand.Create(() => LogStubOperation(Operation.Move));
        RenameCommand = ReactiveCommand.Create(() => LogStubOperation(Operation.Rename));
        DeleteCommand = ReactiveCommand.Create(() => LogStubOperation(Operation.Delete));
        OpenFindCommand = ReactiveCommand.Create(OpenFind);
        OpenBatchRenameCommand = ReactiveCommand.Create(OpenBatchRename);

        RestoreDefaultShortcutsCommand = ReactiveCommand.Create(_configManager.RestoreDefaultShortcuts);
        SetWindowsKeyboardStyleCommand = ReactiveCommand.Create(() => _configManager.SetKeyboardStyle(KeyboardStyle.Windows));
        SetMacKeyboardStyleCommand = ReactiveCommand.Create(() => _configManager.SetKeyboardStyle(KeyboardStyle.MacOS));

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.Copy] = CopyCommand,
            [Operation.Move] = MoveCommand,
            [Operation.Rename] = RenameCommand,
            [Operation.Delete] = DeleteCommand,
            [Operation.SwitchPanel] = ReactiveCommand.Create(SwitchPanel),
            [Operation.OpenFind] = OpenFindCommand,
            [Operation.OpenBatchRename] = OpenBatchRenameCommand,
        };
    }

    private void SetActivePanel(MainPanelViewModel panel)
    {
        ActivePanel = panel;
        LeftPanel.IsActive = panel == LeftPanel;
        RightPanel.IsActive = panel == RightPanel;
    }

    private void SwitchPanel() => SetActivePanel(ActivePanel == LeftPanel ? RightPanel : LeftPanel);

    private void OpenFind() => _findWindowFactory().Show();

    private void OpenBatchRename() => _batchRenameWindowFactory().Show();

    // File operations aren't implemented yet - this just proves menu/toolbar/keyboard all reach
    // the same command. Real ActivePanel-scoped file logic is a later milestone.
    private void LogStubOperation(Operation operation) => log.Info("{0} command executed (stub, ActivePanel={1})", operation, ActivePanel == LeftPanel ? "Left" : "Right");

    // Window-level commands (SwitchPanel/OpenFind/.../Copy stubs) get first refusal; anything not
    // found there falls through to whichever tab is currently active in the active panel - this
    // is how navigation Operations (GoIntoCurrentFolder, GotoFirstItem, ...) reach
    // ItemBrowserViewModel without ShortcutRouter needing to know panels/tabs exist at all.
    public ICommand? GetCommand(Operation operation) =>
        _commands.GetValueOrDefault(operation) ?? ActivePanel?.ActiveTab?.GetCommand(operation);
}
