using System;
using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;
using ReactiveUI;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for FileOperationProgressWindow - the modal dialog "Run Now" shows. It only ever
/// observes a FileOperationJob already enqueued and running on FileOperationQueue's own worker
/// (see that class's doc comment); this window has no execution logic of its own. "Send to
/// Background" closes the dialog without touching the job at all - it just keeps running on the
/// queue, now only visible via JobListWindow. "Cancel" calls Job.Cancel() first. Either way,
/// closing this window never blocks past the click - RequestClose closes it immediately.
/// </summary>
[Observable]
public partial class FileOperationProgressViewModel : IShortcutCommandSource
{
    public FileOperationJob Job { get; }

    public ICommand SendToBackgroundCommand { get; }
    public ICommand CancelCommand { get; }

    public event Action? RequestClose;

    public FileOperationProgressViewModel(FileOperationJob job)
    {
        Job = job;

        SendToBackgroundCommand = ReactiveCommand.Create(() => RequestClose?.Invoke());
        CancelCommand = ReactiveCommand.Create(() =>
        {
            Job.Cancel();
            RequestClose?.Invoke();
        });

        // The job can finish on its own (all items done, or errored out) while this dialog is
        // still open - closes it automatically rather than leaving a "100% done" progress dialog
        // sitting there for the user to dismiss by hand.
        Job.Finished += OnJobFinished;
    }

    private void OnJobFinished() => RequestClose?.Invoke();

    public ICommand? GetCommand(Operation operation) => null;
}
