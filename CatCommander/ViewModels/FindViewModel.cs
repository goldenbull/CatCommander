using System.Windows.Input;
using CatCommander.Config;
using CatCommander.Shortcuts;
using Metalama.Patterns.Observability;

namespace CatCommander.ViewModels;

/// <summary>
/// ViewModel for FindWindow. Empty for now - no operations of its own yet, so GetCommand always
/// answers null; closing the dialog (Escape) is handled directly in FindWindow's code-behind
/// rather than through the Operation table, since it's a universal dialog convention, not a
/// user-configurable shortcut.
/// </summary>
[Observable]
public partial class FindViewModel : IShortcutCommandSource
{
    public ICommand? GetCommand(Operation operation) => null;
}
