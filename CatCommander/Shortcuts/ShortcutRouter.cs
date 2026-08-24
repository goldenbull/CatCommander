using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using CatCommander.Config;
using NLog;

namespace CatCommander.Shortcuts;

/// <summary>
/// Installs the default (Avalonia-native) keyboard shortcut path on a Window: a Tunnel-phase
/// KeyDownEvent handler that fires bound Operations before the event can reach a focused control's
/// own Bubble-phase handling - except for the small set of keys TextEditKeyExceptions reserves for
/// text editing, which are left alone so normal typing/editing isn't disrupted.
///
/// Each Window installs its own instance; there is no single app-wide hook. This gives every
/// Window (including modal dialogs like FindWindow/BatchRenameWindow) its own shortcut scope for
/// free, and makes "which ViewModel answers this Operation" always just "this Window's current
/// DataContext" - see IShortcutCommandSource.
/// </summary>
public static class ShortcutRouter
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    public static void Install(TopLevel window, ShortcutsSettings settings)
    {
        window.AddHandler(
            InputElement.KeyDownEvent,
            (sender, e) => OnKeyDown(window, e, settings),
            RoutingStrategies.Tunnel);
    }

    private static void OnKeyDown(TopLevel window, KeyEventArgs e, ShortcutsSettings settings)
    {
        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        var operation = settings.GetOperation(gesture);
        if (operation == Operation.Nop)
            return;

        var focused = window.FocusManager?.GetFocusedElement();
        if (TextEditKeyExceptions.ShouldYieldToTextEditing(gesture, TextEditKeyExceptions.IsEditableControl(focused)))
            return;

        if (window.DataContext is not IShortcutCommandSource commandSource)
            return;

        var command = commandSource.GetCommand(operation);
        if (command is null || !command.CanExecute(null))
            return;

        command.Execute(null);
        e.Handled = true;
        log.Debug("ShortcutRouter dispatched {0} for {1}", operation, gesture);
    }
}
