using System;
using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;
using ReactiveUI;

namespace CatCommander.ViewModels;

/// <summary>
/// Which of the two ways a confirmed F5/F6 job should present itself - see
/// FileOperationJob's own doc comment for why both are really just different UI presentations of
/// the same FileOperationQueue-driven execution, not two code paths.
/// </summary>
public enum FileOperationMode
{
    RunNow,
    Background,
}

/// <summary>
/// ViewModel for FileOperationConfirmWindow - F5/F6's first, simple confirmation step. Unlike
/// Total Commander, there's no editable destination path field: the destination is always the
/// opposite panel's current directory (see MainWindowViewModel.StartFileOperation), just shown
/// here for confirmation. The three buttons choose Run Now (blocking modal progress), Background
/// (queued, non-modal), or Cancel (do nothing) - see RequestClose.
/// </summary>
[Observable]
public partial class FileOperationConfirmViewModel : IShortcutCommandSource
{
    public FileOperationKind Kind { get; }
    public int ItemCount { get; }
    public string Destination { get; }

    public string Title => Kind == FileOperationKind.Copy ? "Copy" : "Move";

    public string Message =>
        $"{Title} {ItemCount} item{(ItemCount == 1 ? "" : "s")} to:";

    public ICommand RunNowCommand { get; }
    public ICommand BackgroundCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Raised exactly once, with the chosen mode - or null for Cancel/Escape. FileOperationConfirmWindow.axaml.cs
    /// closes itself and hands the value back out through ShowDialog's own result.
    /// </summary>
    public event Action<FileOperationMode?>? RequestClose;

    public FileOperationConfirmViewModel(FileOperationKind kind, int itemCount, string destination)
    {
        Kind = kind;
        ItemCount = itemCount;
        Destination = destination;

        RunNowCommand = ReactiveCommand.Create(() => RequestClose?.Invoke(FileOperationMode.RunNow));
        BackgroundCommand = ReactiveCommand.Create(() => RequestClose?.Invoke(FileOperationMode.Background));
        CancelCommand = ReactiveCommand.Create(() => RequestClose?.Invoke(null));
    }

    public ICommand? GetCommand(Operation operation) => null;
}
