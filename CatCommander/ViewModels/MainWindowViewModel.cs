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

    // Settings action - not an Operation (no keyboard-configurable gesture, no TC precedent), so
    // this is a plain named command only, not registered in _commands/GetCommand. The default
    // keymap itself is hardcoded per-OS (ShortcutsSettings.CurrentStyle) and not user-selectable -
    // this is the only shortcut-related setting exposed in the UI.
    public ICommand RestoreDefaultShortcutsCommand { get; }

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

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.Copy] = CopyCommand,
            [Operation.Move] = MoveCommand,
            [Operation.Rename] = RenameCommand,
            [Operation.Delete] = DeleteCommand,
            [Operation.SwitchPanel] = ReactiveCommand.Create(SwitchPanel),
            [Operation.OpenCurrentFolderInOppositePanel] = ReactiveCommand.Create(OpenCurrentFolderInOppositePanel),
            [Operation.OpenFind] = OpenFindCommand,
            [Operation.OpenBatchRename] = OpenBatchRenameCommand,
        };
    }

    // Reactive direction: called from MainPanel's GotFocus handler (a mouse click, or real focus
    // having already landed here via RequestFocus/ApplyFocus below) to record which panel focus is
    // *already* on. Must never itself call RequestFocus() - GotFocus firing means focus is already
    // exactly where it should be, so re-requesting it here is not just redundant, it's actively
    // dangerous: with two panels each independently self-focusing at startup (see
    // ItemBrowserViewModel.FocusRequested), one panel's RequestFocus stealing focus back would
    // make GotFocus fire on the *other* panel too, which would RequestFocus back, forever - a real
    // infinite ping-pong this app hit and hung on before this method stopped doing that.
    private void SetActivePanel(MainPanelViewModel panel)
    {
        ActivePanel = panel;
        LeftPanel.IsActive = panel == LeftPanel;
        RightPanel.IsActive = panel == RightPanel;
    }

    // Commanded direction: SwitchPanel (Tab) needs to both record the new ActivePanel *and* push
    // real keyboard focus into it - unlike SetActivePanel above, nothing else is going to move
    // focus on its own here.
    private void SwitchPanel()
    {
        var target = ActivePanel == LeftPanel ? RightPanel : LeftPanel;
        SetActivePanel(target);
        target.RequestFocus();
    }

    // Cmd/Ctrl+Left and +Right both do the same thing - "opposite panel" already accounts for
    // direction (whichever panel isn't ActivePanel), so there's nothing left for the two gestures
    // to disambiguate. Opens the *selected* folder in a new tab in the opposite panel - exactly
    // OpenSelectedFolderInNewTab's (Cmd/Ctrl+Up) own logic, aimed across panels instead of within
    // one. Deliberately GetSelectedEnterablePath(), not CurrentPath: CurrentPath is whatever
    // directory the active tab is *browsing*, which is one level up from the highlighted row the
    // user is actually looking at - using it here would open the parent instead of the folder
    // they selected.
    private void OpenCurrentFolderInOppositePanel()
    {
        var path = ActivePanel?.ActiveTab?.GetSelectedEnterablePath();
        if (path is null)
            return;

        var opposite = ActivePanel == LeftPanel ? RightPanel : LeftPanel;
        opposite.OpenNewTab(path);
    }

    private void OpenFind() => _findWindowFactory().Show();

    private void OpenBatchRename() => _batchRenameWindowFactory().Show();

    // File operations aren't implemented yet - this just proves menu/toolbar/keyboard all reach
    // the same command. Real ActivePanel-scoped file logic is a later milestone.
    private void LogStubOperation(Operation operation) => log.Info("{0} command executed (stub, ActivePanel={1})", operation, ActivePanel == LeftPanel ? "Left" : "Right");

    // Window-level commands (SwitchPanel/OpenFind/.../Copy stubs) get first refusal; then
    // panel-scoped ones (OpenSelectedFolderInNewTab - needs the panel's whole Tabs collection, not
    // just the active tab); anything not found there falls through to whichever tab is currently
    // active in the active panel - this is how navigation Operations (GoIntoCurrentFolder,
    // GotoFirstItem, ...) reach ItemBrowserViewModel without ShortcutRouter needing to know
    // panels/tabs exist at all.
    public ICommand? GetCommand(Operation operation) =>
        _commands.GetValueOrDefault(operation)
        ?? ActivePanel?.GetCommand(operation)
        ?? ActivePanel?.ActiveTab?.GetCommand(operation);
}
