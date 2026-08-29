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

    public static void Install(
        TopLevel window,
        ShortcutsSettings settings,
        ShortcutInputContext? inputContext = null,
        ShortcutInputState? inputState = null,
        ShortcutScope scope = ShortcutScope.Operations)
    {
        inputContext?.Track(window, scope);

        // Kept as a passive fallback: when SharpHook handles a configured gesture it suppresses
        // the native event before Avalonia can see it; if the hook is unavailable, this same
        // handler continues to provide every non-OS-reserved shortcut without rebuilding windows.
        window.AddHandler(
            InputElement.KeyDownEvent,
            (sender, e) => OnKeyDown(window, e, settings, inputContext, scope),
            RoutingStrategies.Tunnel);
    }

    private static void OnKeyDown(
        TopLevel window,
        KeyEventArgs e,
        ShortcutsSettings settings,
        ShortcutInputContext? inputContext,
        ShortcutScope scope)
    {
        var gesture = new KeyGesture(e.Key, e.KeyModifiers);
        if (ShortcutRoutingPolicy.ShouldYieldToWindowConvention(gesture, scope))
            return;

        var operation = settings.GetOperation(gesture);
        if (operation == Operation.Nop)
            return;

        var focused = window.FocusManager?.GetFocusedElement();
        var isTextEditing = inputContext?.IsTextEditing == true
                            || TextEditKeyExceptions.IsEditableControl(e.Source)
                            || TextEditKeyExceptions.IsEditableControl(focused);
        if (TextEditKeyExceptions.ShouldYieldToTextEditing(gesture, isTextEditing))
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
