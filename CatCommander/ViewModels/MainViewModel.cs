using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public MainViewModel()
    {
        // Initialize commands with CanExecute observables
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

        log.Info("MainViewModel initialized");
    }

    #region Open Command

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }
    private IObservable<bool> CanExecuteOpenObservable => Observable.Return(CanExecuteOpen());

    private void ExecuteOpen()
    {
        log.Info("Open command executed");
        // TODO: Implement open file/folder logic
    }

    private bool CanExecuteOpen()
    {
        // TODO: Add logic to determine if open command can execute
        return true;
    }

    #endregion

    #region Copy Command

    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
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

    public ReactiveCommand<Unit, Unit> MoveCommand { get; }
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

    public ReactiveCommand<Unit, Unit> RenameCommand { get; }
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

    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
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

    public ReactiveCommand<Unit, Unit> ExpandCurrentFolderCommand { get; }

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

    public ReactiveCommand<Unit, Unit> ExpandSelectedFoldersCommand { get; }

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

    public ReactiveCommand<Unit, Unit> GoIntoCurrentFolderCommand { get; }

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

    public ReactiveCommand<Unit, Unit> GoBackToParentFolderCommand { get; }

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

    public ReactiveCommand<Unit, Unit> GotoFirstItemCommand { get; }
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

    public ReactiveCommand<Unit, Unit> GotoLastItemCommand { get; }
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

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}