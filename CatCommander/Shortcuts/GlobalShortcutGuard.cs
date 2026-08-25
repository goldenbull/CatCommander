using System;
using System.Collections.Generic;
using Avalonia.Input;
using Avalonia.Threading;
using CatCommander.Config;
using CatCommander.Utils;
using NLog;
using SharpHook;
using SharpHook.Data;

namespace CatCommander.Shortcuts;

/// <summary>
/// SharpHook-based patch path for gestures in MacReservedCombos - macOS grabs these at the OS
/// level for its own global shortcuts (Mission Control, etc.) before they ever reach any
/// application, including a focused CatCommander window, so Avalonia's own KeyDownEvent
/// (ShortcutRouter) never sees them. SimpleGlobalHook is the only SharpHook hook type that
/// supports synchronously suppressing an event (KeyboardHookEventArgs.SuppressEvent, set inside
/// the handler) - EventLoopGlobalHook/TaskPoolGlobalHook run handlers on another thread, too late
/// for suppression to take effect.
///
/// This is *not* an app-wide replacement for ShortcutRouter: only gestures in MacReservedCombos
/// are acted on here; everything else is left completely alone (not suppressed, not dispatched)
/// so it flows through the normal path exactly as if this guard didn't exist. The two paths are
/// mutually exclusive per gesture, not competing - see the design plan's flow diagram.
/// </summary>
public sealed class GlobalShortcutGuard : IDisposable
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly SimpleGlobalHook _hook;
    private readonly ShortcutsSettings _settings;
    private readonly Func<IShortcutCommandSource?> _activeCommandSourceProvider;
    private readonly HashSet<KeyCode> _pressedModifiers = new();

    /// <param name="settings">Same ShortcutsSettings instance ShortcutRouter uses - one source of truth.</param>
    /// <param name="activeCommandSourceProvider">
    /// Resolves "whichever ViewModel should currently answer an Operation" - typically the active
    /// window's DataContext. Supplied by the composition root, which is the only place that knows
    /// about the live Window set.
    /// </param>
    public GlobalShortcutGuard(ShortcutsSettings settings, Func<IShortcutCommandSource?> activeCommandSourceProvider)
    {
        _settings = settings;
        _activeCommandSourceProvider = activeCommandSourceProvider;
        _hook = new SimpleGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    /// <summary>
    /// Starts the global hook. No-op on non-macOS platforms - the reserved-combo problem this
    /// patches is macOS-specific (Windows/Linux don't need it; see MacReservedCombos).
    /// </summary>
    public void Start()
    {
        if (!OperatingSystem.IsMacOS())
        {
            log.Info("GlobalShortcutGuard not started: only needed on macOS");
            return;
        }

        try
        {
            _hook.RunAsync();
            log.Info("GlobalShortcutGuard started");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to start GlobalShortcutGuard - reserved macOS combos won't be patched");
        }
    }

    public void Dispose() => _hook.Dispose();

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var code = e.Data.KeyCode;

        if (IsModifier(code))
        {
            _pressedModifiers.Add(code);
            return;
        }

        if (!TryToAvaloniaKey(code, out var key))
            return;

        var gesture = new KeyGesture(key, CurrentModifiers());
        if (!MacReservedCombos.Contains(gesture))
            return;

        var operation = _settings.GetOperation(gesture);
        if (operation == Operation.Nop)
            return;

        // Must check focus before deciding to suppress: if CatCommander isn't the frontmost app,
        // this gesture has nothing to do with us - let macOS/whichever app is focused handle it
        // exactly as it normally would.
        if (!ForegroundAppChecker.IsFrontmostApplication())
            return;

        // Must be set synchronously inside this handler - see the class doc on why SimpleGlobalHook.
        e.SuppressEvent = true;

        Dispatcher.UIThread.Post(() =>
        {
            var command = _activeCommandSourceProvider()?.GetCommand(operation);
            if (command?.CanExecute(null) == true)
                command.Execute(null);
        });

        log.Debug("GlobalShortcutGuard suppressed + dispatched {0} for {1}", operation, gesture);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (IsModifier(e.Data.KeyCode))
            _pressedModifiers.Remove(e.Data.KeyCode);
    }

    private KeyModifiers CurrentModifiers()
    {
        var modifiers = KeyModifiers.None;
        foreach (var code in _pressedModifiers)
        {
            modifiers |= code switch
            {
                KeyCode.VcLeftControl or KeyCode.VcRightControl => KeyModifiers.Control,
                KeyCode.VcLeftAlt or KeyCode.VcRightAlt => KeyModifiers.Alt,
                KeyCode.VcLeftShift or KeyCode.VcRightShift => KeyModifiers.Shift,
                KeyCode.VcLeftMeta or KeyCode.VcRightMeta => KeyModifiers.Meta,
                _ => KeyModifiers.None,
            };
        }

        return modifiers;
    }

    private static bool IsModifier(KeyCode code) => code is
        KeyCode.VcLeftControl or KeyCode.VcRightControl or
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
        KeyCode.VcLeftShift or KeyCode.VcRightShift or
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    /// <summary>
    /// Deliberately small - only the keys MacReservedCombos actually references. Extend this
    /// alongside MacReservedCombos, not ahead of it.
    /// </summary>
    private static bool TryToAvaloniaKey(KeyCode code, out Key key)
    {
        switch (code)
        {
            case KeyCode.VcLeft: key = Key.Left; return true;
            case KeyCode.VcRight: key = Key.Right; return true;
            case KeyCode.VcUp: key = Key.Up; return true;
            case KeyCode.VcDown: key = Key.Down; return true;
            case KeyCode.VcF3: key = Key.F3; return true;
            case KeyCode.VcTab: key = Key.Tab; return true;
            default: key = Key.None; return false;
        }
    }
}
