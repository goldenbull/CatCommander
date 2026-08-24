using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for BatchRenameWindow. Empty for now - see FindViewModel for why GetCommand is a
/// no-op and Escape-to-close isn't routed through here.
/// </summary>
[Observable]
public partial class BatchRenameViewModel : IShortcutCommandSource
{
    public ICommand? GetCommand(Operation operation) => null;
}
