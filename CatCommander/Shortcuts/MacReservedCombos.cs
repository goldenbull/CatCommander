using System.Collections.Generic;
using Avalonia.Input;

namespace CatCommander.Shortcuts;

/// <summary>
/// Key gestures macOS reserves for its own global shortcuts (Mission Control desktop switching,
/// etc.) and consumes before they ever reach any application - including a focused CatCommander
/// window. These are the only gestures GlobalShortcutGuard's SharpHook patch path acts on; every
/// other gesture is left entirely to ShortcutRouter's normal Avalonia-native path.
///
/// Not exhaustive - macOS's actual reserved set also depends on the user's own System Settings
/// customizations, which can't be known ahead of time. This is the known-default set that's likely
/// to collide with Total Commander-style bindings; extend it if a specific gesture is found to be
/// getting swallowed in testing (see the plan's manual verification steps).
/// </summary>
public static class MacReservedCombos
{
    public static readonly HashSet<KeyGesture> All = new()
    {
        new KeyGesture(Key.Left, KeyModifiers.Control),  // Mission Control: previous desktop
        new KeyGesture(Key.Right, KeyModifiers.Control), // Mission Control: next desktop
        new KeyGesture(Key.Up, KeyModifiers.Control),    // Mission Control: show Mission Control
        new KeyGesture(Key.Down, KeyModifiers.Control),  // Mission Control: show App Windows
        new KeyGesture(Key.F3),                          // Mission Control (default, no modifier)
    };

    public static bool Contains(KeyGesture gesture) => All.Contains(gesture);
}
