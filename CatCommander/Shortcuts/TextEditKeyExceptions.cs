using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;

namespace CatCommander.Shortcuts;

/// <summary>
/// Decides whether a key gesture that has an Operation bound to it should still be left for a
/// focused editable control's own (Bubble-phase) handling instead of firing the global Operation.
/// Pure/testable: doesn't touch the visual tree itself, just the two facts a caller already knows.
/// </summary>
public static class TextEditKeyExceptions
{
    /// <summary>
    /// Gestures a text-editing control is expected to own, on both the Ctrl and platform-native
    /// (Cmd on macOS, via KeyModifiers.Meta) modifier for the clipboard/undo ones - CatCommander's
    /// own bindings use "Ctrl+" per Total Commander convention, but native text controls on macOS
    /// respond to Cmd, so both must be excluded from global dispatch while focus is editable.
    /// </summary>
    private static readonly HashSet<KeyGesture> ReservedGestures = new()
    {
        new KeyGesture(Key.Enter),
        new KeyGesture(Key.Escape),
        new KeyGesture(Key.Tab),
        new KeyGesture(Key.Back),
        new KeyGesture(Key.Delete),
        new KeyGesture(Key.Left),
        new KeyGesture(Key.Right),
        new KeyGesture(Key.Up),
        new KeyGesture(Key.Down),
        new KeyGesture(Key.Home),
        new KeyGesture(Key.End),
        new KeyGesture(Key.PageUp),
        new KeyGesture(Key.PageDown),
        new KeyGesture(Key.A, KeyModifiers.Control),
        new KeyGesture(Key.C, KeyModifiers.Control),
        new KeyGesture(Key.V, KeyModifiers.Control),
        new KeyGesture(Key.X, KeyModifiers.Control),
        new KeyGesture(Key.Z, KeyModifiers.Control),
        new KeyGesture(Key.Y, KeyModifiers.Control),
        new KeyGesture(Key.A, KeyModifiers.Meta),
        new KeyGesture(Key.C, KeyModifiers.Meta),
        new KeyGesture(Key.V, KeyModifiers.Meta),
        new KeyGesture(Key.X, KeyModifiers.Meta),
        new KeyGesture(Key.Z, KeyModifiers.Meta),
        new KeyGesture(Key.Y, KeyModifiers.Meta),
    };

    public static bool IsReserved(KeyGesture gesture) => ReservedGestures.Contains(gesture) || gesture.Key is
        Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown;

    /// <summary>
    /// True if the focused element is a text-editing control and the gesture is in the reserved
    /// set - meaning the global shortcut router should not act on it, even though an Operation is
    /// bound to it, and let normal Bubble-phase control handling own the key instead.
    /// </summary>
    public static bool ShouldYieldToTextEditing(KeyGesture gesture, bool focusIsEditable)
        => focusIsEditable && IsReserved(gesture);

    /// <summary>
    /// Whether a given focused element counts as "text-editing" for the rule above.
    /// </summary>
    public static bool IsEditableControl(object? focusedElement) => focusedElement is TextBox;
}
