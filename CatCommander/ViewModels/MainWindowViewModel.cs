using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CatCommander.Browsing;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Resources;
using CatCommander.Services;
using CatCommander.Shortcuts;
using CatCommander.View;
using Metalama.Patterns.Observability;
using ReactiveUI;

namespace CatCommander.ViewModels;

[Observable]
public partial class MainWindowViewModel : IShortcutCommandSource
{
    private static readonly NLog.Logger log = NLog.LogManager.GetCurrentClassLogger();
    private readonly ConfigManager _configManager;
    private readonly FileOperationQueue _fileOperationQueue;
    private readonly ResourceTransferService _transferService;
    private readonly BrowserCommandPolicy _commandPolicy;
    private readonly FileClipboardState? _fileClipboard;
    private readonly ShortcutInputContext? _shortcutInputContext;
    private readonly ShortcutInputState? _shortcutInputState;
    private readonly IEditorPicker? _editorPicker;
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
    public ICommand ChooseEditorCommand { get; }

    /// <summary>
    /// Raised after RestoreDefaultShortcutsCommand rebuilds ShortcutsSettings' effective bindings -
    /// MainWindow.axaml.cs subscribes to re-apply NativeMenuItem.Gesture from the (now-changed)
    /// primary gestures, since a native menu's keyEquivalent is a one-time property set, not
    /// something that re-reads ShortcutsSettings live the way ShortcutRouter's dispatch does.
    /// </summary>
    public event Action? ShortcutsChanged;

    public MainWindowViewModel(
        ConfigManager configManager,
        FileOperationQueue fileOperationQueue,
        Func<MainPanelViewModel> mainPanelFactory,
        Func<FindWindow> findWindowFactory,
        Func<BatchRenameWindow> batchRenameWindowFactory,
        Func<JobListWindow> jobListWindowFactory,
        ResourceTransferService? transferService = null,
        BrowserCommandPolicy? commandPolicy = null,
        ShortcutInputContext? shortcutInputContext = null,
        ShortcutInputState? shortcutInputState = null,
        FileClipboardState? fileClipboard = null,
        IEditorPicker? editorPicker = null)
    {
        _configManager = configManager;
        _fileOperationQueue = fileOperationQueue;
        _transferService = transferService ?? new ResourceTransferService();
        _commandPolicy = commandPolicy ?? new BrowserCommandPolicy();
        _fileClipboard = fileClipboard;
        _shortcutInputContext = shortcutInputContext;
        _shortcutInputState = shortcutInputState;
        _editorPicker = editorPicker;

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

        if (_configManager.LoadSession() is { } session)
        {
            _ = LeftPanel.RestoreSessionAsync(session.Left);
            _ = RightPanel.RestoreSessionAsync(session.Right);
            SetActivePanel(string.Equals(session.ActivePanel, "right", StringComparison.OrdinalIgnoreCase)
                ? RightPanel
                : LeftPanel);
        }

        CopyCommand = ReactiveCommand.CreateFromTask(() => StartFileOperationAsync(FileOperationKind.Copy));
        MoveCommand = ReactiveCommand.CreateFromTask(() => StartFileOperationAsync(FileOperationKind.Move));
        // Rename is a real, tab-level operation now (ItemBrowserViewModel.BeginRenameCurrentItem -
        // F2's in-place grid edit) - this just forwards to whichever tab is active, so the Edit
        // menu/toolbar button (bound to this property) reach the same command F2 does. Not
        // registered in _commands below: doing so would shadow the tab-level command in
        // GetCommand's dispatch chain, since window-level entries get first refusal.
        RenameCommand = ReactiveCommand.Create(() => ActivePanel?.ActiveTab?.GetCommand(Operation.Rename)?.Execute(null));
        DeleteCommand = ReactiveCommand.CreateFromTask(() => StartFileOperationAsync(FileOperationKind.Delete));
        OpenFindCommand = ReactiveCommand.Create(OpenFind);
        OpenBatchRenameCommand = ReactiveCommand.Create(OpenBatchRename);
        OpenCreateDirectoryDialogCommand = ReactiveCommand.Create(OpenCreateDirectoryDialog);
        OpenJobListCommand = ReactiveCommand.Create(OpenJobList);

        RestoreDefaultShortcutsCommand = ReactiveCommand.Create(() =>
        {
            _configManager.RestoreDefaultShortcuts();
            ShortcutsChanged?.Invoke();
        });
        ChooseEditorCommand = ReactiveCommand.CreateFromTask(ChooseEditorAsync);

        _commands = new Dictionary<Operation, ICommand>
        {
            [Operation.Copy] = CopyCommand,
            [Operation.Move] = MoveCommand,
            [Operation.Delete] = DeleteCommand,
            [Operation.CreateDirectory] = OpenCreateDirectoryDialogCommand,
            [Operation.PasteFiles] = ReactiveCommand.CreateFromTask(() => PasteFilesAsync(forceMove: false)),
            [Operation.PasteFilesAsMove] = ReactiveCommand.CreateFromTask(() => PasteFilesAsync(forceMove: true)),
            [Operation.SwitchPanel] = ReactiveCommand.Create(SwitchPanel),
            [Operation.OpenCurrentFolderInLeftPanel] = ReactiveCommand.Create(() => OpenCurrentFolderInPanel(LeftPanel)),
            [Operation.OpenCurrentFolderInRightPanel] = ReactiveCommand.Create(() => OpenCurrentFolderInPanel(RightPanel)),
            [Operation.OpenFind] = OpenFindCommand,
            [Operation.OpenBatchRename] = OpenBatchRenameCommand,
        };
    }

    private async Task ChooseEditorAsync()
    {
        if (_editorPicker is null)
        {
            log.Warn("Choose F4 Editor was invoked without an editor picker service");
            return;
        }

        if (await _editorPicker.PickAsync() is not { Length: > 0 } command)
        {
            log.Info("Choose F4 Editor completed without a usable local path");
            return;
        }

        _configManager.Settings.Editor.Command = command.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        log.Info("F4 editor changed to {0}; saving configuration", _configManager.Settings.Editor.Command);
        _configManager.SaveSettings();
        log.Info("F4 editor configuration save completed; runtime value is {0}",
            _configManager.Settings.Editor.Command);
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

    public SessionState CaptureSession() => new()
    {
        ActivePanel = ActivePanel == RightPanel ? "right" : "left",
        Left = LeftPanel.CaptureSession(),
        Right = RightPanel.CaptureSession(),
    };

    // Commanded direction: SwitchPanel (Tab) needs to both record the new ActivePanel *and* push
    // real keyboard focus into it - unlike SetActivePanel above, nothing else is going to move
    // focus on its own here.
    private void SwitchPanel()
    {
        var target = ActivePanel == LeftPanel ? RightPanel : LeftPanel;
        SetActivePanel(target);
        target.RequestFocus();
    }

    // Direction stays explicit all the way from gesture to command: Left targets LeftPanel and is
    // valid only when RightPanel is active; Right is the mirror image. Deliberately uses the
    // selected enterable resource, not CurrentPath: CurrentPath is whatever
    // directory the active tab is *browsing*, which is one level up from the highlighted row the
    // user is actually looking at - using it here would open the parent instead of the folder
    // they selected.
    private void OpenCurrentFolderInPanel(MainPanelViewModel targetPanel)
    {
        if (ActivePanel == targetPanel)
            return;

        var resource = ActivePanel?.ActiveTab?.GetSelectedEnterableResource();
        if (resource is null)
            return;

        targetPanel.OpenNewTab(resource.Value);
    }

    private void OpenFind() => _findWindowFactory().Show();

    private void OpenBatchRename() => _batchRenameWindowFactory().Show();

    // F7 - constructed directly rather than via the DI Func<T> factory pattern the other windows
    // use (see Program.cs), since NewFolderViewModel needs a runtime parameter (the active tab's
    // own CreateDirectoryAsync) that plain DI factories don't thread through.
    private void OpenCreateDirectoryDialog()
    {
        if (ActivePanel?.ActiveTab is not { } tab || !_commandPolicy.CanCreateDirectory(tab.Context))
            return;

        var viewModel = new NewFolderViewModel(tab.CreateDirectoryAsync);
        new NewFolderWindow(viewModel, _configManager.Shortcuts, _shortcutInputContext, _shortcutInputState).Show();
    }

    // F5/F6/Delete - see FileOperationQueue's own doc comment for why "blocking" vs "background"
    // are just two presentations of the same queued execution, not two different code paths here.
    // The destination (Copy/Move only - Delete has none) is always the opposite panel's *own*
    // current directory (its ActiveTab's CurrentPath, not GetSelectedEnterablePath() - unlike
    // OpenCurrentFolderInPanel above, this isn't about a selected row, it's "wherever that
    // panel is already browsing").
    private async Task StartFileOperationAsync(FileOperationKind kind)
    {
        if (ActivePanel?.ActiveTab is not { } sourceTab)
            return;

        var targets = sourceTab.GetOperationBrowserItems();
        if (targets.Count == 0)
            return;

        ContainerRef? destination = null;
        MainPanelViewModel? destinationPanel = null;

        if (kind != FileOperationKind.Delete)
        {
            destinationPanel = ActivePanel == LeftPanel ? RightPanel : LeftPanel;
            if (destinationPanel.ActiveTab?.WritableDestination is not { } writableDestination)
                return;

            destination = writableDestination;
        }

        await ConfirmAndQueueFileOperationAsync(kind, targets, destination, sourceTab, destinationPanel);
    }

    private Task PasteFilesAsync(bool forceMove)
    {
        if (_fileClipboard is not { Items.Count: > 0 } clipboard ||
            ActivePanel?.ActiveTab?.WritableDestination is not { } destination)
            return Task.CompletedTask;

        var kind = forceMove || clipboard.MoveOnPaste ? FileOperationKind.Move : FileOperationKind.Copy;
        var snapshot = clipboard.Items;
        return ConfirmAndQueueFileOperationAsync(
            kind, snapshot, destination, clipboard.SourceTab, ActivePanel,
            clearClipboardAfterSuccess: kind == FileOperationKind.Move ? () => clipboard.ClearIfCurrent(snapshot) : null);
    }

    private async Task ConfirmAndQueueFileOperationAsync(
        FileOperationKind kind,
        IReadOnlyList<BrowserItem> targets,
        ContainerRef? destination,
        ItemBrowserViewModel? sourceTab,
        MainPanelViewModel? destinationPanel,
        Action? clearClipboardAfterSuccess = null)
    {
        if (!_commandPolicy.CanRunFileOperation(kind, targets, destination))
            return;

        if (GetActiveWindow() is not { } owner)
            return;

        var confirmViewModel = new FileOperationConfirmViewModel(
            kind,
            targets.Count,
            destination is { } target ? GetDisplayPath(target.Resource) : null);
        var confirmWindow = new FileOperationConfirmWindow(
            confirmViewModel,
            _configManager.Shortcuts,
            _shortcutInputContext,
            _shortcutInputState);
        var mode = await confirmWindow.ShowDialog<FileOperationMode?>(owner);
        if (mode is null)
            return;

        var job = new FileOperationJob(kind, targets, destination, _transferService);
        job.Finished += () =>
        {
            RefreshAfterFileOperation(sourceTab, destinationPanel, destination);
            if (job.Status == FileOperationJobStatus.Completed)
                clearClipboardAfterSuccess?.Invoke();
        };
        _fileOperationQueue.Enqueue(job);

        if (mode == FileOperationMode.RunNow)
        {
            var progressViewModel = new FileOperationProgressViewModel(job);
            var progressWindow = new FileOperationProgressWindow(
                progressViewModel,
                _configManager.Shortcuts,
                _shortcutInputContext,
                _shortcutInputState);
            await progressWindow.ShowDialog(owner);
        }
        else
        {
            OpenJobList();
        }
    }

    // Move/Delete make items disappear from the source listing (Delete: gone entirely; Move: gone
    // from here, appear at the destination); Copy only does the latter, and Delete has no
    // destination at all - refreshing the source unconditionally and the destination only if one
    // exists is simplest and harmless (NavigateToAsync re-lists whatever's actually there either
    // way). The destination tab is only refreshed if it's still showing the same directory the job
    // was aimed at - the user may have navigated that panel elsewhere while a background job was
    // running, and forcibly yanking them back to `destination` would be a worse surprise than a
    // listing that's one refresh stale.
    private static void RefreshAfterFileOperation(
        ItemBrowserViewModel? sourceTab,
        MainPanelViewModel? destinationPanel,
        ContainerRef? destination)
    {
        if (sourceTab is not null)
            _ = sourceTab.RefreshListingAfterFileOperationAsync();

        if (destination is { } target &&
            destinationPanel?.ActiveTab is { } destinationTab &&
            !ReferenceEquals(destinationTab, sourceTab) &&
            destinationTab.Context?.Location is { } displayed &&
            displayed.Provider.IsSameFileSystem(target.Resource.Provider) &&
            displayed.Provider.PathComparer.Equals(displayed.Path, target.Resource.Path))
            _ = destinationTab.RefreshListingAfterFileOperationAsync();
    }

    private static string GetDisplayPath(ResourceRef resource) =>
        resource.Provider is IExternalPathProvider external
            ? external.GetExternalPath(resource.Path)
            : resource.Path;

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
    public ICommand? GetCommand(Operation operation)
    {
        if ((operation == Operation.OpenCurrentFolderInLeftPanel && ActivePanel == LeftPanel) ||
            (operation == Operation.OpenCurrentFolderInRightPanel && ActivePanel == RightPanel))
        {
            return null;
        }

        if (ActivePanel?.ActiveTab is { } tab && operation is
            Operation.Copy or Operation.Move or Operation.Delete or Operation.CreateDirectory)
        {
            var targets = tab.GetOperationBrowserItems();
            var destination = operation is Operation.Copy or Operation.Move
                ? (ActivePanel == LeftPanel ? RightPanel : LeftPanel).ActiveTab?.WritableDestination
                : null;

            var available = operation switch
            {
                Operation.Copy => _commandPolicy.CanCopy(targets, destination),
                Operation.Move => _commandPolicy.CanMove(targets, destination),
                Operation.Delete => _commandPolicy.CanDelete(targets),
                Operation.CreateDirectory => _commandPolicy.CanCreateDirectory(tab.Context),
                _ => true,
            };

            if (!available)
                return null;
        }

        if (operation is Operation.PasteFiles or Operation.PasteFilesAsMove)
        {
            if (_fileClipboard is not { Items.Count: > 0 } clipboard ||
                ActivePanel?.ActiveTab?.WritableDestination is not { } destination)
                return null;

            var kind = operation == Operation.PasteFilesAsMove || clipboard.MoveOnPaste
                ? FileOperationKind.Move
                : FileOperationKind.Copy;
            if (!_commandPolicy.CanRunFileOperation(kind, clipboard.Items, destination))
                return null;
        }

        return _commands.GetValueOrDefault(operation)
            ?? ActivePanel?.GetCommand(operation)
            ?? ActivePanel?.ActiveTab?.GetCommand(operation);
    }
}
