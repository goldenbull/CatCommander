using System;
using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using CatCommander.Configuration;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public MainViewModel()
    {
        log.Info("MainViewModel initialized");

        // Initialize existing commands
        OpenCommand = ReactiveCommand.Create(ExecuteOpen);

        // Initialize shortcut commands
        CopyCommand = ReactiveCommand.Create(ExecuteCopy);
        MoveCommand = ReactiveCommand.Create(ExecuteMove);
        RenameCommand = ReactiveCommand.Create(ExecuteRename);
        DeleteCommand = ReactiveCommand.Create(ExecuteDelete);
        ExpandCurrentFolderCommand = ReactiveCommand.Create(ExecuteExpandCurrentFolder);
        ExpandSelectedFoldersCommand = ReactiveCommand.Create(ExecuteExpandSelectedFolders);
        GoIntoCurrentFolderCommand = ReactiveCommand.Create(ExecuteGoIntoCurrentFolder);
        GoBackToParentFolderCommand = ReactiveCommand.Create(ExecuteGoBackToParentFolder);
        GotoFirstItemCommand = ReactiveCommand.Create(ExecuteGotoFirstItem);
        GotoLastItemCommand = ReactiveCommand.Create(ExecuteGotoLastItem);
    }

    #region Existing Commands

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    private void ExecuteOpen()
    {
        log.Info("Open command executed");
        // TODO: Implement open file/folder logic
    }

    #endregion

    #region Shortcut Commands

    public ReactiveCommand<Unit, Unit> CopyCommand { get; }
    public ReactiveCommand<Unit, Unit> MoveCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }
    public ReactiveCommand<Unit, Unit> ExpandCurrentFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ExpandSelectedFoldersCommand { get; }
    public ReactiveCommand<Unit, Unit> GoIntoCurrentFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> GoBackToParentFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> GotoFirstItemCommand { get; }
    public ReactiveCommand<Unit, Unit> GotoLastItemCommand { get; }

    private void ExecuteCopy()
    {
        log.Info("Copy command executed");
        // TODO: Implement copy logic
    }

    private void ExecuteMove()
    {
        log.Info("Move command executed");
        // TODO: Implement move logic
    }

    private void ExecuteRename()
    {
        log.Info("Rename command executed");
        // TODO: Implement rename logic
    }

    private void ExecuteDelete()
    {
        log.Info("Delete command executed");
        // TODO: Implement delete logic
    }

    private void ExecuteExpandCurrentFolder()
    {
        log.Info("ExpandCurrentFolder command executed");
        // TODO: Implement expand current folder logic
    }

    private void ExecuteExpandSelectedFolders()
    {
        log.Info("ExpandSelectedFolders command executed");
        // TODO: Implement expand selected folders logic
    }

    private void ExecuteGoIntoCurrentFolder()
    {
        log.Info("GoIntoCurrentFolder command executed");
        // TODO: Implement go into current folder logic
    }

    private void ExecuteGoBackToParentFolder()
    {
        log.Info("GoBackToParentFolder command executed");
        // TODO: Implement go back to parent folder logic
    }

    private void ExecuteGotoFirstItem()
    {
        log.Info("GotoFirstItem command executed");
        // TODO: Implement goto first item logic
    }

    private void ExecuteGotoLastItem()
    {
        log.Info("GotoLastItem command executed");
        // TODO: Implement goto last item logic
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
