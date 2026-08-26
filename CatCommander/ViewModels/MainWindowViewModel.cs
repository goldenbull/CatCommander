using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CatCommander.Config;
using CatCommander.Models;
using CatCommander.Services;
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
    private readonly FileOperationQueue _fileOperationQueue;
    private readonly Func<FindWindow> _findWindowFactory;
    private readonly Func<BatchRenameWindow> _batchRenameWindowFactory;
    private readonly Func<JobListWindow> _jobListWindowFactory;
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
    public ICommand OpenCreateDirectoryDialogCommand { get; }
    public ICommand OpenJobListCommand { get; }

    // Settings action - not an Operation (no keyboard-configurable gesture, no TC precedent), so
    // this is a plain named command only, not registered in _commands/GetCommand. The default
    // keymap itself is hardcoded per-OS (ShortcutsSettings.CurrentStyle) and not user-selectable -
    // this is the only shortcut-related setting exposed in the UI.
    public ICommand RestoreDefaultShortcutsCommand { get; }

    public MainWindowViewModel(
        ConfigManager configManager,
        FileOperationQueue fileOperationQueue,
        Func<MainPanelViewModel> mainPanelFactory,
        Func<FindWindow> findWindowFactory,
        Func<BatchRenameWindow> batchRenameWindowFactory,
        Func<JobListWindow> jobListWindowFactory)
    {
        _configManager = configManager;
        _fileOperationQueue = fileOperationQueue;

        // Two distinct instances of the same type - needs a factory, not direct constructor
        // injection, same reasoning as MainPanelViewModel needing one for ItemBrowserViewModel.
        LeftPanel = mainPanelFactory();
        RightPanel = mainPanelFactory();
        _findWindowFactory = findWindowFactory;
        _batchRenameWindowFactory = batchRenameWindowFactory;
        _jobListWindowFactory = jobListWindowFactory;

        LeftPanel.OnActivated = () => SetActivePanel(LeftPanel);
        RightPanel.OnActivated = () => SetActivePanel(RightPanel);
        SetActivePanel(LeftPanel);

        CopyCommand = ReactiveCommand.CreateFromTask(() => StartFileOperationAsync(FileOperationKind.Copy));
        MoveCommand = ReactiveCommand.CreateFromTask(() => StartFileOperationAsync(FileOperationKind.Move));
        // Rename is a real, tab-level operation now (ItemBrowserViewModel.BeginRenameCurrentItem -
        // F2's in-place grid edit) - this just forwards to whichever tab is active, so the Edit
        // menu/toolbar button (bound to this property) reach the same command F2 does. Not
        // registered in _commands below: doing so would shadow the tab-level command in
        // GetCommand's dispatch chain, since window-level entries get first refusal.
        RenameCommand = ReactiveCommand.Create(() => ActivePanel?.ActiveTab?.GetCommand(Operation.Rename)?.Execute(null));
        DeleteCommand = ReactiveCommand.Create(() => LogStubFileOperation(Operation.Delete));
        OpenFindCommand = ReactiveCommand.Create(OpenFind);
        OpenBatchRenameCommand = ReactiveCommand.Create(OpenBatchRename);
        OpenCreateDirectoryDialogCommand = ReactiveCommand.Create(OpenCreateDirectoryDialog);
        OpenJobListCommand = ReactiveCommand.Create(OpenJobList);

        RestoreDefaultShortcutsCommand = ReactiveCommand.Create(_configManager.RestoreDefaultShortcuts);

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.Copy] = CopyCommand,
            [Operation.Move] = MoveCommand,
            [Operation.Delete] = DeleteCommand,
            [Operation.CreateDirectory] = OpenCreateDirectoryDialogCommand,
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

    // F7 - constructed directly rather than via the DI Func<T> factory pattern the other windows
    // use (see Program.cs), since NewFolderViewModel needs a runtime parameter (the active tab's
    // own CreateDirectoryAsync) that plain DI factories don't thread through.
    private void OpenCreateDirectoryDialog()
    {
        if (ActivePanel?.ActiveTab is not { } tab)
            return;

        var viewModel = new NewFolderViewModel(tab.CreateDirectoryAsync);
        new NewFolderWindow(viewModel, _configManager.Shortcuts).Show();
    }

    // Delete isn't implemented yet - this just proves menu/toolbar/keyboard all reach the same
    // command. Copy/Move went through this same stub until StartFileOperationAsync replaced it;
    // Rename doesn't go through here at all: renaming several items at once needs a pattern,
    // which is what OpenBatchRename is for, not a bare "Rename" invocation.
    private void LogStubFileOperation(Operation operation)
    {
        var targets = ActivePanel?.ActiveTab?.GetOperationTargets() ?? Array.Empty<IFileSystemItem>();
        log.Info(
            "{0} command executed (stub, ActivePanel={1}, targets=[{2}])",
            operation,
            ActivePanel == LeftPanel ? "Left" : "Right",
            string.Join(", ", targets.Select(t => t.Name)));
    }

    // F5/F6 - see FileOperationQueue's own doc comment for why "blocking" vs "background" are
    // just two presentations of the same queued execution, not two different code paths here.
    // The destination is always the opposite panel's *own* current directory (its ActiveTab's
    // CurrentPath, not GetSelectedEnterablePath() - unlike OpenCurrentFolderInOppositePanel above,
    // this isn't about a selected row, it's "wherever that panel is already browsing").
    private async Task StartFileOperationAsync(FileOperationKind kind)
    {
        if (ActivePanel?.ActiveTab is not { } sourceTab)
            return;

        var targets = sourceTab.GetOperationTargets();
        if (targets.Count == 0 || sourceTab.Provider is not { } provider)
            return;

        var destinationPanel = ActivePanel == LeftPanel ? RightPanel : LeftPanel;
        if (destinationPanel.ActiveTab is not { } destinationTab || string.IsNullOrEmpty(destinationTab.CurrentPath))
            return;

        var destination = destinationTab.CurrentPath;
        if (GetActiveWindow() is not { } owner)
            return;

        var confirmViewModel = new FileOperationConfirmViewModel(kind, targets.Count, destination);
        var confirmWindow = new FileOperationConfirmWindow(confirmViewModel, _configManager.Shortcuts);
        var mode = await confirmWindow.ShowDialog<FileOperationMode?>(owner);
        if (mode is null)
            return;

        var job = new FileOperationJob(kind, targets, destination, provider);
        job.Finished += () => RefreshAfterFileOperation(sourceTab, destinationPanel, destination);
        _fileOperationQueue.Enqueue(job);

        if (mode == FileOperationMode.RunNow)
        {
            var progressViewModel = new FileOperationProgressViewModel(job);
            var progressWindow = new FileOperationProgressWindow(progressViewModel, _configManager.Shortcuts);
            await progressWindow.ShowDialog(owner);
        }
        else
        {
            OpenJobList();
        }
    }

    // Move makes items disappear from the source listing and appear at the destination; Copy only
    // does the latter - refreshing both unconditionally is simplest and harmless (NavigateToAsync
    // re-lists whatever's actually there either way). The destination tab is only refreshed if
    // it's still showing the same directory the job was aimed at - the user may have navigated
    // that panel elsewhere while a background job was running, and forcibly yanking them back to
    // `destination` would be a worse surprise than a listing that's one refresh stale.
    private static void RefreshAfterFileOperation(ItemBrowserViewModel sourceTab, MainPanelViewModel destinationPanel, string destination)
    {
        _ = sourceTab.NavigateToAsync(sourceTab.CurrentPath);

        if (destinationPanel.ActiveTab is { } destinationTab && destinationTab.CurrentPath == destination)
            _ = destinationTab.NavigateToAsync(destination);
    }

    private void OpenJobList() => _jobListWindowFactory().Show();

    // MainWindowViewModel deliberately holds no Window reference of its own (see
    // OpenCreateDirectoryDialog's doc comment on why NewFolderWindow/etc. are constructed
    // directly) - a real *modal* dialog (FileOperationConfirmWindow/FileOperationProgressWindow's
    // ShowDialog, unlike Find/BatchRename/NewFolder's plain Show()) still needs an owner Window,
    // so this looks one up the same way App.axaml.cs's own GlobalShortcutGuard callback does.
    private static Window? GetActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        return desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
    }

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
