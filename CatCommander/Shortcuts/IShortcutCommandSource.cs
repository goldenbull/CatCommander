using System.Windows.Input;
using CatCommander.Config;

namespace CatCommander.Shortcuts;

/// <summary>
/// Implemented by a Window's DataContext (ViewModel) to expose which ICommand, if any, answers
/// a given Operation. Both keyboard dispatch paths (ShortcutRouter's Avalonia Tunnel path and
/// GlobalShortcutGuard's primary SharpHook path) resolve commands through this single interface -
/// there's exactly one dispatch endpoint regardless of which path a keystroke came from.
/// </summary>
public interface IShortcutCommandSource
{
    ICommand? GetCommand(Operation operation);
}
