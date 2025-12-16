using System;
using System.Reactive;
using System.Reactive.Linq;
using CatCommander.ViewModels;
using NLog;
using ReactiveUI;

namespace CatCommander.Commands;

/// <summary>
/// Central command manager that handles all keyboard and mouse commands
/// for the file commander application. Implemented as a singleton.
/// </summary>
public sealed class CommandExecutor
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();
    private static readonly Lazy<CommandExecutor> _instance = new(() => new CommandExecutor());

    /// <summary>
    /// Gets the singleton instance of CommandExecutor
    /// </summary>
    public static CommandExecutor Instance => _instance.Value;

    /// <summary>
    /// The main window view model that contains the panels
    /// </summary>
    public MainWindowViewModel? MainWindowViewModel { get; set; }

    /// <summary>
    /// Gets the currently active panel
    /// </summary>
    private MainPanelViewModel? ActivePanel => MainWindowViewModel?.LeftPanel.IsActive == true
        ? MainWindowViewModel?.LeftPanel
        : MainWindowViewModel?.RightPanel;

    private CommandExecutor()
    {
        InitializeCommands();
        log.Info("CommandExecutor singleton initialized");
    }

    #region Command Properties

    // File operation commands
    public ReactiveCommand<Unit, Unit> OpenCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> CopyCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> MoveCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> RenameCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExpandCurrentFolderCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> ExpandSelectedFoldersCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GoIntoCurrentFolderCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GoBackToParentFolderCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GotoFirstItemCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> GotoLastItemCommand { get; private set; } = null!;

    // Panel navigation commands
    public ReactiveCommand<Unit, Unit> RefreshCommand { get; private set; } = null!;
    public ReactiveCommand<Unit, Unit> NavigateUpCommand { get; private set; } = null!;
    public ReactiveCommand<string, Unit> NavigateToCommand { get; private set; } = null!;

    #endregion

    #region Command Initialization

    private void InitializeCommands()
    {
        // File operation commands
        OpenCommand = ReactiveCommand.Create(ExecuteOpen, CanExecuteOpenObservable, RxApp.MainThreadScheduler);
        CopyCommand = ReactiveCommand.Create(ExecuteCopy, CanExecuteCopyObservable, RxApp.MainThreadScheduler);
        MoveCommand = ReactiveCommand.Create(ExecuteMove, CanExecuteMoveObservable, RxApp.MainThreadScheduler);
        RenameCommand = ReactiveCommand.Create(ExecuteRename, CanExecuteRenameObservable, RxApp.MainThreadScheduler);
        DeleteCommand = ReactiveCommand.Create(ExecuteDelete, CanExecuteDeleteObservable, RxApp.MainThreadScheduler);
        ExpandCurrentFolderCommand = ReactiveCommand.Create(ExecuteExpandCurrentFolder,
            CanExecuteExpandCurrentFolderObservable, RxApp.MainThreadScheduler);
        ExpandSelectedFoldersCommand = ReactiveCommand.Create(ExecuteExpandSelectedFolders,
            CanExecuteExpandSelectedFoldersObservable, RxApp.MainThreadScheduler);
        GoIntoCurrentFolderCommand = ReactiveCommand.Create(ExecuteGoIntoCurrentFolder,
            CanExecuteGoIntoCurrentFolderObservable, RxApp.MainThreadScheduler);
        GoBackToParentFolderCommand = ReactiveCommand.Create(ExecuteGoBackToParentFolder,
            CanExecuteGoBackToParentFolderObservable, RxApp.MainThreadScheduler);
        GotoFirstItemCommand = ReactiveCommand.Create(ExecuteGotoFirstItem, CanExecuteGotoFirstItemObservable,
            RxApp.MainThreadScheduler);
        GotoLastItemCommand = ReactiveCommand.Create(ExecuteGotoLastItem, CanExecuteGotoLastItemObservable,
            RxApp.MainThreadScheduler);

        // Panel navigation commands
        RefreshCommand = ReactiveCommand.Create(ExecuteRefresh, CanExecuteRefreshObservable, RxApp.MainThreadScheduler);
        NavigateUpCommand = ReactiveCommand.Create(ExecuteNavigateUp, CanExecuteNavigateUpObservable, RxApp.MainThreadScheduler);
        NavigateToCommand = ReactiveCommand.Create<string>(ExecuteNavigateTo, outputScheduler: RxApp.MainThreadScheduler);
    }

    #endregion

    #region Open Command

    private IObservable<bool> CanExecuteOpenObservable => Observable.Return(CanExecuteOpen());

    private void ExecuteOpen()
    {
        log.Info("Open command executed");
        // TODO: Implement open file/folder logic
        // Example: Access view model properties
        // var currentPath = _viewModel.SomeProperty;
        // _viewModel.SomeMethod();
    }

    private bool CanExecuteOpen()
    {
        // TODO: Add logic to determine if open command can execute
        // Example: Check view model state
        // return _viewModel.HasSelection;
        return true;
    }

    #endregion

    #region Copy Command

    private IObservable<bool> CanExecuteCopyObservable => Observable.Return(CanExecuteCopy());

    private void ExecuteCopy()
    {
        log.Info("Copy command executed");
        // TODO: Implement copy logic
    }

    private bool CanExecuteCopy()
    {
        // TODO: Add logic to determine if copy command can execute (e.g., items selected)
        return true;
    }

    #endregion

    #region Move Command

    private IObservable<bool> CanExecuteMoveObservable => Observable.Return(CanExecuteMove());

    private void ExecuteMove()
    {
        log.Info("Move command executed");
        // TODO: Implement move logic
    }

    private bool CanExecuteMove()
    {
        // TODO: Add logic to determine if move command can execute (e.g., items selected)
        return true;
    }

    #endregion

    #region Rename Command

    private IObservable<bool> CanExecuteRenameObservable => Observable.Return(CanExecuteRename());

    private void ExecuteRename()
    {
        log.Info("Rename command executed");
        // TODO: Implement rename logic
    }

    private bool CanExecuteRename()
    {
        // TODO: Add logic to determine if rename command can execute (e.g., single item selected)
        return true;
    }

    #endregion

    #region Delete Command

    private IObservable<bool> CanExecuteDeleteObservable => Observable.Return(CanExecuteDelete());

    private void ExecuteDelete()
    {
        log.Info("Delete command executed");
        // TODO: Implement delete logic
    }

    private bool CanExecuteDelete()
    {
        // TODO: Add logic to determine if delete command can execute (e.g., items selected)
        return true;
    }

    #endregion

    #region ExpandCurrentFolder Command

    private IObservable<bool> CanExecuteExpandCurrentFolderObservable =>
        Observable.Return(CanExecuteExpandCurrentFolder());

    private void ExecuteExpandCurrentFolder()
    {
        log.Info("ExpandCurrentFolder command executed");
        // TODO: Implement expand current folder logic
    }

    private bool CanExecuteExpandCurrentFolder()
    {
        // TODO: Add logic to determine if expand command can execute (e.g., current item is a folder)
        return true;
    }

    #endregion

    #region ExpandSelectedFolders Command

    private IObservable<bool> CanExecuteExpandSelectedFoldersObservable =>
        Observable.Return(CanExecuteExpandSelectedFolders());

    private void ExecuteExpandSelectedFolders()
    {
        log.Info("ExpandSelectedFolders command executed");
        // TODO: Implement expand selected folders logic
    }

    private bool CanExecuteExpandSelectedFolders()
    {
        // TODO: Add logic to determine if expand command can execute (e.g., folders selected)
        return true;
    }

    #endregion

    #region GoIntoCurrentFolder Command

    private IObservable<bool> CanExecuteGoIntoCurrentFolderObservable =>
        Observable.Return(CanExecuteGoIntoCurrentFolder());

    private void ExecuteGoIntoCurrentFolder()
    {
        log.Info("GoIntoCurrentFolder command executed");
        // TODO: Implement go into current folder logic
    }

    private bool CanExecuteGoIntoCurrentFolder()
    {
        // TODO: Add logic to determine if command can execute (e.g., current item is a folder)
        return true;
    }

    #endregion

    #region GoBackToParentFolder Command

    private IObservable<bool> CanExecuteGoBackToParentFolderObservable =>
        Observable.Return(CanExecuteGoBackToParentFolder());

    private void ExecuteGoBackToParentFolder()
    {
        log.Info("GoBackToParentFolder command executed");
        // TODO: Implement go back to parent folder logic
    }

    private bool CanExecuteGoBackToParentFolder()
    {
        // TODO: Add logic to determine if command can execute (e.g., not at root)
        return true;
    }

    #endregion

    #region GotoFirstItem Command

    private IObservable<bool> CanExecuteGotoFirstItemObservable => Observable.Return(CanExecuteGotoFirstItem());

    private void ExecuteGotoFirstItem()
    {
        log.Info("GotoFirstItem command executed");
        // TODO: Implement goto first item logic
    }

    private bool CanExecuteGotoFirstItem()
    {
        // TODO: Add logic to determine if command can execute (e.g., items exist)
        return true;
    }

    #endregion

    #region GotoLastItem Command

    private IObservable<bool> CanExecuteGotoLastItemObservable => Observable.Return(CanExecuteGotoLastItem());

    private void ExecuteGotoLastItem()
    {
        log.Info("GotoLastItem command executed");
        // TODO: Implement goto last item logic
    }

    private bool CanExecuteGotoLastItem()
    {
        // TODO: Add logic to determine if command can execute (e.g., items exist)
        return true;
    }

    #endregion

    #region Refresh Command

    private IObservable<bool> CanExecuteRefreshObservable => Observable.Return(CanExecuteRefresh());

    private void ExecuteRefresh()
    {
        log.Info("Refresh command executed");
        ActivePanel?.Refresh();
    }

    private bool CanExecuteRefresh()
    {
        return ActivePanel != null;
    }

    #endregion

    #region NavigateUp Command

    private IObservable<bool> CanExecuteNavigateUpObservable => Observable.Return(CanExecuteNavigateUp());

    private void ExecuteNavigateUp()
    {
        log.Info("NavigateUp command executed");
        ActivePanel?.NavigateUp();
    }

    private bool CanExecuteNavigateUp()
    {
        return ActivePanel != null && !ActivePanel.IsAtRoot;
    }

    #endregion

    #region NavigateTo Command

    private void ExecuteNavigateTo(string path)
    {
        log.Info($"NavigateTo command executed: {path}");
        ActivePanel?.NavigateToPath(path);
    }

    #endregion
}
