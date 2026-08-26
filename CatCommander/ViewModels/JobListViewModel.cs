using System.Collections.ObjectModel;
using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Services;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for JobListWindow - the non-modal "job list" window F5/F6's Background mode queues
/// into (see FileOperationQueue's own doc comment). Jobs is the queue's own collection, not a
/// copy - every job it ever holds, past and present, so this doubles as a simple history view
/// once a job completes, not just an in-progress queue.
/// </summary>
[Observable]
public partial class JobListViewModel : IShortcutCommandSource
{
    public ObservableCollection<FileOperationJob> Jobs { get; }

    public JobListViewModel(FileOperationQueue queue)
    {
        Jobs = queue.Jobs;
    }

    public ICommand? GetCommand(Operation operation) => null;
}
