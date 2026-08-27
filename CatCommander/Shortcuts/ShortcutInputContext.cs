using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CatCommander.Shortcuts;

public enum ShortcutScope
{
    Operations,
    Dialog,
}

public static class ShortcutRoutingPolicy
{
    public static bool ShouldYieldToWindowConvention(KeyGesture gesture, ShortcutScope scope)
        => scope == ShortcutScope.Dialog
           && gesture.KeyModifiers == KeyModifiers.None
           && gesture.Key is Key.Enter or Key.Escape;
}

/// <summary>
/// Thread-safe focus snapshot for the SharpHook callback. The hook thread must not inspect the
/// Avalonia visual tree synchronously while deciding whether to suppress an event.
/// </summary>
public sealed class ShortcutInputContext
{
    private int _isTextEditing;
    private int _activeDialogConventionCount;

    public bool IsTextEditing => Volatile.Read(ref _isTextEditing) != 0;
    public bool HasActiveDialogConventions => Volatile.Read(ref _activeDialogConventionCount) > 0;

    public bool ShouldYieldToActiveWindowConvention(KeyGesture gesture)
        => gesture is { Key: Key.Escape, KeyModifiers: KeyModifiers.None }
           || ShortcutRoutingPolicy.ShouldYieldToWindowConvention(
               gesture,
               HasActiveDialogConventions ? ShortcutScope.Dialog : ShortcutScope.Operations);

    public void Track(TopLevel topLevel, ShortcutScope scope = ShortcutScope.Operations)
    {
        topLevel.AddHandler(
            InputElement.GotFocusEvent,
            (_, e) => Volatile.Write(
                ref _isTextEditing,
                TextEditKeyExceptions.IsEditableControl(e.Source) ? 1 : 0),
            RoutingStrategies.Tunnel);

        if (scope != ShortcutScope.Dialog || topLevel is not Window window)
            return;

        var isActive = false;
        void Enter()
        {
            if (isActive)
                return;
            isActive = true;
            Interlocked.Increment(ref _activeDialogConventionCount);
        }

        void Leave()
        {
            if (!isActive)
                return;
            isActive = false;
            Interlocked.Decrement(ref _activeDialogConventionCount);
        }

        window.Activated += (_, _) => Enter();
        window.Deactivated += (_, _) => Leave();
        window.Closed += (_, _) => Leave();
    }
}

public sealed class ShortcutInputState
{
    public bool LowLevelHookActive { get; set; }
}
