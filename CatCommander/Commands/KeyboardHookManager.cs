using System;
using System.Collections.Generic;
using Avalonia.Input;
using NLog;
using SharpHook;
using SharpHook.Data;

/*
There are two keyboard event type systems:

Avalonia has two enums: Avalonia.Input.KeyModifiers and Avalonia.Input.Key,
which are used in Avalonia.Input.KeyEventArgs class.
This is quite simple and straightforward.

SharpHook works in a lower layer, and has more details.
1. KeyPressed and KeyReleased events are triggered separately,
2. Alt, Control, Meta, Shift keys distinguish between left and right
think about the complex series of key events:
  left alt down, right alt down, left alt up, letter 'A' down, right alt up, letter 'A' up
practically, an 'alt+A' event should raise on letter 'A' down
so we need to track each low level key event and raise events to app layer properly

In config file, key shortcuts are in string format, but we use a normalized record struct internally for performance

*/

namespace CatCommander.Commands;

/// <summary>
/// internal record struct for key events
/// we use SharpHook KeyCode directly to avoid unnecessary SharpHook KeyCode to Avalonia Key translation
/// </summary>
public record CatKeyEventArgs(KeyModifiers Modifiers, KeyCode Key)
{
    public override string ToString()
    {
        return KeyboardHookManager.NormalizedString(this);
    }
}

/// <summary>
/// Manages low-level global keyboard hooks using SharpHook.
/// This allows capturing keyboard events that are normally handled by the OS.
/// Singleton pattern ensures only one instance manages global hooks.
/// </summary>
public class KeyboardHookManager : IDisposable
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    #region Singleton

    private static KeyboardHookManager? _instance;

    public static KeyboardHookManager Instance
    {
        get
        {
            _instance ??= new KeyboardHookManager();
            return _instance;
        }
    }

    #endregion

    // underlying object
    private readonly SimpleGlobalHook _globalHook;

    // keep track of current modifiers status, distinguish left and right
    private readonly HashSet<KeyCode> _pressedModifiers = new();
    private KeyModifiers _modifiers = KeyModifiers.None;

    // Map KeyCode to KeyModifiers for efficient modifier tracking
    private static readonly Dictionary<KeyCode, KeyModifiers> ModifierKeyMap = new()
    {
        { KeyCode.VcLeftControl, KeyModifiers.Control },
        { KeyCode.VcRightControl, KeyModifiers.Control },
        { KeyCode.VcLeftAlt, KeyModifiers.Alt },
        { KeyCode.VcRightAlt, KeyModifiers.Alt },
        { KeyCode.VcLeftShift, KeyModifiers.Shift },
        { KeyCode.VcRightShift, KeyModifiers.Shift },
        { KeyCode.VcLeftMeta, KeyModifiers.Meta },
        { KeyCode.VcRightMeta, KeyModifiers.Meta }
    };

    // raise event to app layer
    public event EventHandler<CatKeyEventArgs>? KeyPressed;

    private KeyboardHookManager()
    {
        _globalHook = new SimpleGlobalHook();
        _globalHook.KeyPressed += OnKeyPressed;
        _globalHook.KeyReleased += OnKeyReleased;

        log.Info("KeyboardHookManager initialized");
    }

    public void Start()
    {
        try
        {
            _globalHook.RunAsync();
            log.Info("Global keyboard hook started");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to start global keyboard hook");
            throw;
        }
    }

    private void Stop()
    {
        try
        {
            _globalHook.Dispose();
            log.Info("Global keyboard hook stopped");
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error stopping global keyboard hook");
        }
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        var keyCode = e.Data.KeyCode;

        // Track modifier keys
        if (ModifierKeyMap.ContainsKey(keyCode))
        {
            UpdateModifiers(keyCode, true);
            return;
        }

        // not modifier keys, raise events
        var eventArgs = new CatKeyEventArgs(_modifiers, keyCode);
        log.Debug($"Key pressed: {eventArgs} (e.Data: {e.Data})");
        KeyPressed?.Invoke(this, eventArgs);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (ModifierKeyMap.ContainsKey(e.Data.KeyCode))
        {
            UpdateModifiers(e.Data.KeyCode, false);
        }
    }

    private void UpdateModifiers(KeyCode code, bool isPressed)
    {
        if (isPressed)
            _pressedModifiers.Add(code);
        else
            _pressedModifiers.Remove(code);
        UpdateKeyModifiers();
    }

    private void UpdateKeyModifiers()
    {
        _modifiers = KeyModifiers.None;
        foreach (var keyCode in _pressedModifiers)
        {
            if (ModifierKeyMap.TryGetValue(keyCode, out var modifier))
            {
                _modifiers |= modifier;
            }
            else
            {
                throw new SystemException($"unexpected: Modifier {keyCode} not found");
            }
        }
    }

    /// <summary>
    /// get the normalized string representation
    /// </summary>
    public static string NormalizedString(CatKeyEventArgs evt)
    {
        // Build normalized string: modifiers in consistent order + main key
        var result = new List<string>();

        // Add modifiers in consistent order
        if (evt.Modifiers.HasFlag(KeyModifiers.Control)) result.Add("ctrl");
        if (evt.Modifiers.HasFlag(KeyModifiers.Alt)) result.Add("alt");
        if (evt.Modifiers.HasFlag(KeyModifiers.Shift)) result.Add("shift");
        if (evt.Modifiers.HasFlag(KeyModifiers.Meta)) result.Add("meta");

        // Add the main key at the end
        var keyStr = evt.Key.ToString().ToLowerInvariant();
        if (keyStr.StartsWith("vc")) keyStr = keyStr.Substring(2);
        result.Add(keyStr);

        return string.Join("+", result);
    }

    /// <summary>
    /// Normalizes a key combination string to an internal CatKeyEventArgs record
    /// </summary>
    public static CatKeyEventArgs? Parse(string shortcutsStr)
    {
        if (string.IsNullOrWhiteSpace(shortcutsStr))
            return null;

        // Split by '+' and process each part
        var modifiers = KeyModifiers.None;
        var keyCode = KeyCode.VcUndefined;
        var parts = shortcutsStr.Split('+', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var keyStr = part.Trim().ToLowerInvariant();

            // Check if it's a modifier
            if (keyStr == "ctrl" || keyStr == "control")
            {
                modifiers |= KeyModifiers.Control;
            }
            else if (keyStr == "alt")
            {
                modifiers |= KeyModifiers.Alt;
            }
            else if (keyStr == "shift")
            {
                modifiers |= KeyModifiers.Shift;
            }
            else if (keyStr == "meta" || keyStr == "cmd" || keyStr == "command")
            {
                modifiers |= KeyModifiers.Meta;
            }
            else
            {
                // It's the main key (take the last non-modifier)
                // parse KeyStr to SharpHook.KeyCode
                if (!Enum.TryParse("vc" + keyStr, true, out keyCode))
                {
                    throw new ArgumentException($"can not recognize key: {keyStr}");
                }
            }
        }

        return new CatKeyEventArgs(modifiers, keyCode);
    }
}