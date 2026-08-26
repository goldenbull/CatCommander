using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;
using ReactiveUI;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for NewFolderWindow (F7). Takes the actual directory-creation callback as a
/// constructor parameter (ItemBrowserViewModel.CreateDirectoryAsync, bound to whichever tab was
/// active when the dialog was opened) rather than going through DI - see
/// MainWindowViewModel.OpenCreateDirectoryDialog, which constructs this directly instead of via
/// the Func&lt;T&gt; factory pattern the rest of the app's windows use, since that pattern doesn't
/// support a runtime parameter like this.
/// </summary>
[Observable]
public partial class NewFolderViewModel : IShortcutCommandSource
{
    private readonly Func<string, Task> _createDirectory;

    public string FolderName { get; set; } = string.Empty;

    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    /// <summary>
    /// Raised on both Create and Cancel - NewFolderWindow.axaml.cs subscribes with Close().
    /// </summary>
    public event Action? RequestClose;

    public NewFolderViewModel(Func<string, Task> createDirectory)
    {
        _createDirectory = createDirectory;

        CreateCommand = ReactiveCommand.CreateFromTask(CreateAsync);
        CancelCommand = ReactiveCommand.Create(() => RequestClose?.Invoke());
    }

    private async Task CreateAsync()
    {
        var name = FolderName.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        await _createDirectory(name);
        RequestClose?.Invoke();
    }

    public ICommand? GetCommand(Operation operation) => null;
}
