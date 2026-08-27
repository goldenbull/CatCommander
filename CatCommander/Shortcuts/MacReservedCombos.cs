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

        // Native window-tab cycling ("Show Next/Previous Tab", the same feature Safari/Finder/
        // Xcode use): AppKit's NSWindow intercepts Ctrl+Tab for this at the window-chrome level -
        // confirmed by testing (a real keypress logged as plain "Tab", not "Ctrl+Tab": AppKit
        // consumes the Control modifier before Avalonia's own input pipeline ever sees it) - even
        // though CatCommander is a single-window app with no native tab group to cycle to.
        new KeyGesture(Key.Tab, KeyModifiers.Control),   // AppKit: show next/previous window tab

        // Cmd+. is Mac OS's own long-standing system-wide "Cancel" gesture (predates Cocoa) -
        // AppKit's NSApplication generically intercepts it as an implicit Escape/abort signal, not
        // something tied to any specific app registering it. Confirmed by testing: GlobalShortcutGuard's
        // own SharpHook hook (strictly lower-level than Avalonia's input pipeline) logs seeing the
        // raw keystroke every time, but ShortcutRouter's own log never once fires for it - Ctrl+.
        // and Alt+. both reach ShortcutRouter normally, only the Meta (Cmd) modifier is swallowed.
        new KeyGesture(Key.OemPeriod, KeyModifiers.Meta), // AppKit: system-wide Cancel gesture
    };

    public static bool Contains(KeyGesture gesture) => All.Contains(gesture);
}
