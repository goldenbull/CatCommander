using System;
using System.ComponentModel;
using System.Reactive;
using System.Runtime.CompilerServices;
using NLog;
using ReactiveUI;

namespace CatCommander.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public MainViewModel()
    {
        log.Info("MainViewModel initialized");
        OpenCommand = ReactiveCommand.Create(ExecuteOpen);
    }

    public ReactiveCommand<Unit, Unit> OpenCommand { get; }

    private void ExecuteOpen()
    {
        log.Info("Open command executed");
        // TODO: Implement open file/folder logic
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
