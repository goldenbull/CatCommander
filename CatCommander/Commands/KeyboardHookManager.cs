using System;
using System.Collections.Generic;
using Avalonia.Input;
using NLog;
using SharpHook;
using SharpHook.Data;

namespace CatCommander.Commands;

public class CatKeyEventArgs
{
    public required KeyModifiers Modifiers { get; init; }
    public required KeyCode KeyCode { get; init; }

    public override string ToString()
    {
        var parts = new List<string>();

        // Add modifiers in consistent order
        if (Modifiers.HasFlag(KeyModifiers.Control))
            parts.Add("Ctrl");
        if (Modifiers.HasFlag(KeyModifiers.Alt))
            parts.Add("Alt");
        if (Modifiers.HasFlag(KeyModifiers.Shift))
            parts.Add("Shift");
        if (Modifiers.HasFlag(KeyModifiers.Meta))
            parts.Add("Meta");

        // Add the key code
        parts.Add(KeyCode.ToString());

        var keyCombination = string.Join("+", parts);

        // Normalize the result
        return KeyboardHookManager.Normalized(keyCombination);
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

    private readonly SimpleGlobalHook _globalHook;
    private readonly HashSet<KeyCode> _pressedModifiers = new();

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

    public void Stop()
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

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        // Track modifier keys
        UpdateModifiers(e.Data.KeyCode, true);

        // Build key modifiers and event args
        var modifiers = BuildKeyModifiers();
        var eventArgs = new CatKeyEventArgs
        {
            Modifiers = modifiers,
            KeyCode = e.Data.KeyCode
        };

        log.Debug($"Key pressed: {eventArgs} (e.Data: {e.Data})");

        // Raise event for subscribers
        if (!IsModifierKey(e.Data.KeyCode))
        {
            // do not raise events on modifier key only
            KeyPressed?.Invoke(this, eventArgs);
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        // Update modifier keys
        UpdateModifiers(e.Data.KeyCode, false);
    }

    private void UpdateModifiers(KeyCode code, bool isPressed)
    {
        if (IsModifierKey(code))
        {
            if (isPressed)
                _pressedModifiers.Add(code);
            else
                _pressedModifiers.Remove(code);
        }
    }

    private bool IsModifierKey(KeyCode code)
    {
        return ModifierKeyMap.ContainsKey(code);
    }

    private KeyModifiers BuildKeyModifiers()
    {
        var modifiers = KeyModifiers.None;
        foreach (var keyCode in _pressedModifiers)
        {
            if (ModifierKeyMap.TryGetValue(keyCode, out var modifier))
            {
                modifiers |= modifier;
            }
        }
        return modifiers;
    }

    /// <summary>
    /// Normalizes a key combination string to a uniform format for comparison.
    /// Converts different representations (ctrl+alt+a, Alt+Ctrl+A, Alt+Ctrl+VcA) to a consistent format.
    /// </summary>
    /// <param name="keyCombination">The key combination string to normalize</param>
    /// <returns>Normalized string in the format: ctrl+alt+shift+meta+key</returns>
    public static string Normalized(string keyCombination)
    {
        if (string.IsNullOrWhiteSpace(keyCombination))
            return string.Empty;

        // Split by '+' and process each part
        var parts = keyCombination.Split('+', StringSplitOptions.RemoveEmptyEntries);
        var modifiers = new HashSet<string>();
        string? mainKey = null;

        foreach (var part in parts)
        {
            var normalized = part.Trim().ToLowerInvariant();

            // Remove 'Vc' prefix if present (e.g., VcA -> a, VcTab -> tab)
            if (normalized.StartsWith("vc"))
                normalized = normalized.Substring(2);

            // Check if it's a modifier
            if (normalized == "ctrl" || normalized == "control")
            {
                modifiers.Add("ctrl");
            }
            else if (normalized == "alt")
            {
                modifiers.Add("alt");
            }
            else if (normalized == "shift")
            {
                modifiers.Add("shift");
            }
            else if (normalized == "meta" || normalized == "cmd" || normalized == "command")
            {
                modifiers.Add("meta");
            }
            else
            {
                // It's the main key (take the last non-modifier)
                mainKey = normalized;
            }
        }

        // Build normalized string: modifiers in consistent order + main key
        var result = new List<string>();

        // Add modifiers in consistent order
        if (modifiers.Contains("ctrl")) result.Add("ctrl");
        if (modifiers.Contains("alt")) result.Add("alt");
        if (modifiers.Contains("shift")) result.Add("shift");
        if (modifiers.Contains("meta")) result.Add("meta");

        // Add the main key at the end
        if (!string.IsNullOrEmpty(mainKey))
            result.Add(mainKey);

        return string.Join("+", result);
    }

    public void Dispose()
    {
        Stop();
    }
}