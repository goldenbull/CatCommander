using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CatCommander.View;

/// <summary>
/// Dialog-window keyboard conventions every small `Window` in this app wants (Escape-to-close,
/// Enter-submits-the-default-button), factored out so each window's code-behind states its intent
/// in one line instead of hand-rolling its own Tunnel-phase `KeyDown` handler. Dialog windows
/// install `ShortcutRouter` with `ShortcutScope.Dialog`, so both the Avalonia and SharpHook input
/// paths yield plain Enter/Escape to these handlers.
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
        => window.InstallEnterSubmits(() => command.Execute(null), () => command.CanExecute(null));

    public static void InstallEnterSubmits(this Window window, Action submit, Func<bool>? canSubmit = null)
    {
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Handled || e.Key != Key.Enter || canSubmit?.Invoke() == false)
                return;

            submit();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }
}
