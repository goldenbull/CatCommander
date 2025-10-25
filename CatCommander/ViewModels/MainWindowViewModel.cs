using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using CatCommander.Commands;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();
    private readonly CommandManager _commandManager;

    public MainWindowViewModel()
    {
        _commandManager = new CommandManager(this);
        log.Info("MainWindowViewModel initialized");
    }

    #region Commands - Delegated to CommandManager

    public ReactiveCommand<Unit, Unit> OpenCommand => _commandManager.OpenCommand;
    public ReactiveCommand<Unit, Unit> CopyCommand => _commandManager.CopyCommand;
    public ReactiveCommand<Unit, Unit> MoveCommand => _commandManager.MoveCommand;
    public ReactiveCommand<Unit, Unit> RenameCommand => _commandManager.RenameCommand;
    public ReactiveCommand<Unit, Unit> DeleteCommand => _commandManager.DeleteCommand;
    public ReactiveCommand<Unit, Unit> ExpandCurrentFolderCommand => _commandManager.ExpandCurrentFolderCommand;
    public ReactiveCommand<Unit, Unit> ExpandSelectedFoldersCommand => _commandManager.ExpandSelectedFoldersCommand;
    public ReactiveCommand<Unit, Unit> GoIntoCurrentFolderCommand => _commandManager.GoIntoCurrentFolderCommand;
    public ReactiveCommand<Unit, Unit> GoBackToParentFolderCommand => _commandManager.GoBackToParentFolderCommand;
    public ReactiveCommand<Unit, Unit> GotoFirstItemCommand => _commandManager.GotoFirstItemCommand;
    public ReactiveCommand<Unit, Unit> GotoLastItemCommand => _commandManager.GotoLastItemCommand;

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}