using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CatCommander.Browsing;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;
using Metalama.Patterns.Observability;

namespace CatCommander.ViewModels;

public enum FileOperationKind
{
    Copy,
    Move,
    Delete,
}

public enum FileOperationJobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// One F5/F6/Delete batch: a snapshot of the items GetOperationTargets() returned at the moment
/// the user confirmed, a fixed destination directory for Copy/Move (the opposite panel's
/// CurrentPath at that same moment - never re-resolved later, so the job's target can't drift
/// under it; null for Delete, which has no destination), and the provider to run everything
/// through (only ever LocalFileSystemProvider today - see IFileSystemProvider).
///
/// Only ever driven by FileOperationQueue's background worker calling RunAsync - never called
/// directly from the UI. Every property mutation below is posted through Dispatcher.UIThread
/// because RunAsync always executes on that worker's background thread, not the UI thread; the
/// [Observable] aspect's PropertyChanged notifications (and anything bound to them - the
/// blocking-mode FileOperationProgressWindow, the non-modal JobListWindow) need to fire there.
/// </summary>
[Observable]
public partial class FileOperationJob
{
    private readonly ResourceTransferService _transferService;

    public FileOperationKind Kind { get; }
    public IReadOnlyList<BrowserItem> Items { get; }
    public ContainerRef? Destination { get; }

    // Items/Kind/Destination are fixed for the job's whole lifetime (set once here, in the
    // constructor, never after) - this computed property's value can't go stale, so it doesn't
    // need [Observable]'s change notification the way the explicitly-set properties below do.
    //
    // A plain method, not an expression-bodied property directly branching on Kind/Items - the
    // Observable aspect's dependency analysis of a conditional expression whose *condition* reads
    // one property (Kind) and whose *branches* read another (Items) misparses them as a single
    // "Kind.Items" member-access chain to watch (a real Metalama bug, not anything meaningful in
    // this code) and fails to compile. Description itself just forwards to this method, which the
    // aspect doesn't attempt to deep-analyze the same way.
    public string Description => BuildDescription();

    private string BuildDescription()
    {
        var itemWord = Items.Count == 1 ? "item" : "items";
        return Kind == FileOperationKind.Delete
            ? $"Delete {Items.Count} {itemWord}"
            : $"{Kind} {Items.Count} {itemWord} to {Destination?.Resource.Path}";
    }

    public FileOperationJobStatus Status { get; set; } = FileOperationJobStatus.Queued;
    public int CompletedCount { get; set; }
    public int TotalCount => Items.Count;
    public string CurrentItemName { get; set; } = string.Empty;
    public string CurrentDetail { get; set; } = string.Empty;
    public string? ErrorSummary { get; set; }

    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Raised (on the UI thread) once RunAsync has fully finished, however it ended - success,
    /// per-item errors, or Cancel(). MainWindowViewModel subscribes to refresh both panels
    /// (StartFileOperation), and FileOperationProgressViewModel subscribes to auto-close the
    /// blocking-mode progress window.
    /// </summary>
    public event Action? Finished;

    public FileOperationJob(
        FileOperationKind kind,
        IReadOnlyList<BrowserItem> items,
        ContainerRef? destination,
        ResourceTransferService transferService)
    {
        Kind = kind;
        Items = items;
        Destination = destination;
        _transferService = transferService;
    }

    // Compatibility constructor for callers/tests being migrated from the original single-provider
    // job model. New code should pass BrowserItems so heterogeneous projected results keep their
    // provider provenance.
    public FileOperationJob(
        FileOperationKind kind,
        IReadOnlyList<IFileSystemItem> items,
        string? destination,
        IFileSystemProvider provider)
        : this(
            kind,
            items.Select(item => new BrowserItem(
                item,
                new ResourceRef(provider, item.FullPath),
                null,
                provider.ResourceCapabilities)).ToList(),
            destination is null
                ? null
                : new ContainerRef(new ResourceRef(provider, destination), provider.ContainerCapabilities),
            new ResourceTransferService())
    {
    }

    /// <summary>
    /// Cancels this job. Safe to call whether it's still queued or already running -
    /// CancellationTokenSource itself handles both; RunAsync just stops advancing to the next item
    /// once it notices.
    /// </summary>
    public void Cancel() => _cts.Cancel();

    public async Task RunAsync()
    {
        Dispatcher.UIThread.Post(() => Status = FileOperationJobStatus.Running);

        var errors = new List<string>();
        var completed = 0;

        foreach (var item in Items)
        {
            if (_cts.IsCancellationRequested)
                break;

            var itemName = item.Item.Name;
            Dispatcher.UIThread.Post(() => CurrentItemName = itemName);

            var progress = new Progress<string>(path => Dispatcher.UIThread.Post(() => CurrentDetail = path));

            try
            {
                if (Kind == FileOperationKind.Copy)
                    await _transferService.CopyAsync(item, Destination!.Value, progress, _cts.Token);
                else if (Kind == FileOperationKind.Move)
                    await _transferService.MoveAsync(item, Destination!.Value, progress, _cts.Token);
                else
                    await _transferService.DeleteAsync(item, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                errors.Add($"{item.Item.Name}: {ex.Message}");
            }

            completed++;
            var completedSnapshot = completed;
            Dispatcher.UIThread.Post(() => CompletedCount = completedSnapshot);
        }

        var finalStatus = _cts.IsCancellationRequested
            ? FileOperationJobStatus.Cancelled
            : errors.Count > 0 ? FileOperationJobStatus.Failed : FileOperationJobStatus.Completed;
        var errorSummary = errors.Count > 0 ? string.Join("; ", errors) : null;

        Dispatcher.UIThread.Post(() =>
        {
            if (errorSummary is not null)
                ErrorSummary = errorSummary;
            Status = finalStatus;
            Finished?.Invoke();
        });
    }
}
