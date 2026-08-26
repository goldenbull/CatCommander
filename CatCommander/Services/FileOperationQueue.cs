using System.Collections.ObjectModel;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Threading;
using CatCommander.ViewModels;

namespace CatCommander.Services;

/// <summary>
/// The "system-level job list" F5/F6 jobs go through - a single DI singleton (Program.cs) shared
/// by MainWindowViewModel.StartFileOperation (which enqueues), the blocking-mode
/// FileOperationProgressWindow (which just observes a job already running here rather than
/// driving it itself - see FileOperationJob.RunAsync's own doc comment) and the non-modal
/// JobListWindow (which shows Jobs directly). Jobs run strictly one at a time, in the order
/// they were enqueued, on a single background worker loop - "background/job-list mode" and
/// "blocking mode" are purely how the UI presents an already-running job, never two different
/// execution paths.
/// </summary>
public class FileOperationQueue
{
    public ObservableCollection<FileOperationJob> Jobs { get; } = new();

    private readonly Channel<FileOperationJob> _pending = Channel.CreateUnbounded<FileOperationJob>();

    public FileOperationQueue()
    {
        _ = Task.Run(ProcessLoopAsync);
    }

    public void Enqueue(FileOperationJob job)
    {
        Dispatcher.UIThread.Post(() => Jobs.Add(job));
        _pending.Writer.TryWrite(job);
    }

    private async Task ProcessLoopAsync()
    {
        await foreach (var job in _pending.Reader.ReadAllAsync())
            await job.RunAsync();
    }
}
