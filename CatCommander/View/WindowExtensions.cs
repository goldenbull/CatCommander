using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CatCommander.View;

/// <summary>
/// Dialog-window keyboard conventions every small `Window` in this app wants (Escape-to-close,
/// Enter-submits-the-default-button), factored out so each window's code-behind states its intent
/// in one line instead of hand-rolling its own Tunnel-phase `KeyDown` handler. Both install
/// Tunnel-phase, after `ShortcutRouter.Install` (called first in every constructor) - a real
/// `Operation` bound to Escape or Enter, if any, still gets first refusal.
///
/// Neither of these is a user-configurable `Operation`: they're universal dialog conventions, not
/// keyboard shortcuts someone would want to rebind. `TextEditKeyExceptions` already keeps
/// `ShortcutRouter` from stealing Enter/Escape while a `TextBox` has focus, which is exactly why a
/// `TextBox`'s own focus never lets Enter reach a `Window`-level `IsDefault` button on its own -
/// `InstallEnterSubmits` is what actually closes that gap.
/// </summary>
public static class WindowExtensions
{
    public static void InstallEscapeToClose(this Window window, Action? onEscape = null)
    {
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Handled || e.Key != Key.Escape)
                return;

            (onEscape ?? window.Close)();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    public static void InstallEnterSubmits(this Window window, ICommand command)
    {
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Handled || e.Key != Key.Enter || !command.CanExecute(null))
                return;

            command.Execute(null);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
}
