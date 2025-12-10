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
    private readonly CommandExecutor _commandExecutor;

    public MainWindowViewModel()
    {
        _commandExecutor = new CommandExecutor(this);
        log.Info("MainWindowViewModel initialized");
    }

    #region Commands - Delegated to CommandManager

    public ReactiveCommand<Unit, Unit> OpenCommand => _commandExecutor.OpenCommand;
    public ReactiveCommand<Unit, Unit> CopyCommand => _commandExecutor.CopyCommand;
    public ReactiveCommand<Unit, Unit> MoveCommand => _commandExecutor.MoveCommand;
    public ReactiveCommand<Unit, Unit> RenameCommand => _commandExecutor.RenameCommand;
    public ReactiveCommand<Unit, Unit> DeleteCommand => _commandExecutor.DeleteCommand;
    public ReactiveCommand<Unit, Unit> ExpandCurrentFolderCommand => _commandExecutor.ExpandCurrentFolderCommand;
    public ReactiveCommand<Unit, Unit> ExpandSelectedFoldersCommand => _commandExecutor.ExpandSelectedFoldersCommand;
    public ReactiveCommand<Unit, Unit> GoIntoCurrentFolderCommand => _commandExecutor.GoIntoCurrentFolderCommand;
    public ReactiveCommand<Unit, Unit> GoBackToParentFolderCommand => _commandExecutor.GoBackToParentFolderCommand;
    public ReactiveCommand<Unit, Unit> GotoFirstItemCommand => _commandExecutor.GotoFirstItemCommand;
    public ReactiveCommand<Unit, Unit> GotoLastItemCommand => _commandExecutor.GotoLastItemCommand;

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}