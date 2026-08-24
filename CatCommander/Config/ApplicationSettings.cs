using System;
using Tomlyn.Serialization;

namespace CatCommander.Config;

/// <summary>
/// Which primary modifier key convention the default shortcuts use - not a font/theme choice,
/// this changes what "primary modifier" means in ShortcutsSettings.GetDefaults (Ctrl on Windows,
/// Cmd/Meta on macOS), since real macOS users expect Cmd for the shortcuts that would use Ctrl
/// on Windows. See ShortcutsSettings for the one deliberate exception (SwitchTabInSamePanel).
/// </summary>
public enum KeyboardStyle
{
    Windows,
    MacOS,
}

/// <summary>
/// General application settings, persisted to app.toml via ConfigManager.
/// </summary>
public class ApplicationSettings
{
    // Stored as a string, not the enum directly: Tomlyn's default reflection serializer writes
    // enums as their raw numeric value ("KeyboardStyle = 1"), which isn't legible in a
    // user-editable config file. KeyboardStyle below is the actual property code should use.
    [TomlPropertyName("keyboard_style")]
    public string KeyboardStyleName { get; set; } = DefaultKeyboardStyle.ToString();

    [TomlIgnore]
    public KeyboardStyle KeyboardStyle
    {
        get => Enum.TryParse<KeyboardStyle>(KeyboardStyleName, ignoreCase: true, out var value)
            ? value
            : DefaultKeyboardStyle;
        set => KeyboardStyleName = value.ToString();
    }

    private static KeyboardStyle DefaultKeyboardStyle => OperatingSystem.IsMacOS() ? KeyboardStyle.MacOS : KeyboardStyle.Windows;
}
