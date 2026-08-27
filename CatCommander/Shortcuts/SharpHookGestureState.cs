using System.Collections.Generic;
using Avalonia.Input;
using SharpHook.Data;

namespace CatCommander.Shortcuts;

/// <summary>
/// Pure key-state adapter between SharpHook and Avalonia. It deliberately knows nothing about
/// windows, commands, or dispatchers, so native callbacks cannot accidentally enter UI state.
/// </summary>
public sealed class SharpHookGestureState
{
    private readonly HashSet<KeyCode> _pressedModifiers = new();

    public bool TryPress(KeyCode code, out KeyGesture gesture)
    {
        if (IsModifier(code))
        {
            _pressedModifiers.Add(code);
            gesture = default!;
            return false;
        }

        if (!TryToAvaloniaKey(code, out var key))
        {
            gesture = default!;
            return false;
        }

        gesture = new KeyGesture(key, CurrentModifiers());
        return true;
    }

    public void Release(KeyCode code)
    {
        if (IsModifier(code))
            _pressedModifiers.Remove(code);
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

    private static bool TryToAvaloniaKey(KeyCode code, out Key key)
    {
        key = code switch
        {
            KeyCode.VcLeft => Key.Left, KeyCode.VcRight => Key.Right,
            KeyCode.VcUp => Key.Up, KeyCode.VcDown => Key.Down,
            KeyCode.VcTab => Key.Tab, KeyCode.VcPeriod => Key.OemPeriod,
            KeyCode.VcComma => Key.OemComma, KeyCode.VcMinus => Key.OemMinus,
            KeyCode.VcEquals => Key.OemPlus, KeyCode.VcSemicolon => Key.OemSemicolon,
            KeyCode.VcQuote => Key.OemQuotes, KeyCode.VcBackQuote => Key.OemTilde,
            KeyCode.VcBackslash => Key.OemBackslash, KeyCode.VcSlash => Key.OemQuestion,
            KeyCode.VcOpenBracket => Key.OemOpenBrackets,
            KeyCode.VcCloseBracket => Key.OemCloseBrackets,
            KeyCode.VcA => Key.A, KeyCode.VcB => Key.B, KeyCode.VcC => Key.C,
            KeyCode.VcD => Key.D, KeyCode.VcE => Key.E, KeyCode.VcF => Key.F,
            KeyCode.VcG => Key.G, KeyCode.VcH => Key.H, KeyCode.VcI => Key.I,
            KeyCode.VcJ => Key.J, KeyCode.VcK => Key.K, KeyCode.VcL => Key.L,
            KeyCode.VcM => Key.M, KeyCode.VcN => Key.N, KeyCode.VcO => Key.O,
            KeyCode.VcP => Key.P, KeyCode.VcQ => Key.Q, KeyCode.VcR => Key.R,
            KeyCode.VcS => Key.S, KeyCode.VcT => Key.T, KeyCode.VcU => Key.U,
            KeyCode.VcV => Key.V, KeyCode.VcW => Key.W, KeyCode.VcX => Key.X,
            KeyCode.VcY => Key.Y, KeyCode.VcZ => Key.Z,
            KeyCode.Vc0 => Key.D0, KeyCode.Vc1 => Key.D1, KeyCode.Vc2 => Key.D2,
            KeyCode.Vc3 => Key.D3, KeyCode.Vc4 => Key.D4, KeyCode.Vc5 => Key.D5,
            KeyCode.Vc6 => Key.D6, KeyCode.Vc7 => Key.D7, KeyCode.Vc8 => Key.D8,
            KeyCode.Vc9 => Key.D9,
            KeyCode.VcF1 => Key.F1, KeyCode.VcF2 => Key.F2, KeyCode.VcF3 => Key.F3,
            KeyCode.VcF4 => Key.F4, KeyCode.VcF5 => Key.F5, KeyCode.VcF6 => Key.F6,
            KeyCode.VcF7 => Key.F7, KeyCode.VcF8 => Key.F8, KeyCode.VcF9 => Key.F9,
            KeyCode.VcF10 => Key.F10, KeyCode.VcF11 => Key.F11, KeyCode.VcF12 => Key.F12,
            KeyCode.VcEnter => Key.Enter, KeyCode.VcEscape => Key.Escape,
            KeyCode.VcBackspace => Key.Back, KeyCode.VcDelete => Key.Delete,
            KeyCode.VcHome => Key.Home, KeyCode.VcEnd => Key.End,
            KeyCode.VcPageUp => Key.PageUp, KeyCode.VcPageDown => Key.PageDown,
            KeyCode.VcSpace => Key.Space,
            _ => Key.None,
        };
        return key != Key.None;
    }
}
