using System;
using System.IO;
using NLog;
using Tomlyn;

namespace CatCommander.Config;

/// <summary>
/// Loads/saves keymap.toml. Registered in DI (not a singleton) - see App composition root.
/// </summary>
public class ConfigManager
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly string _keymapConfigFilePath;

    public ShortcutsSettings Shortcuts { get; private set; } = new();

    public ConfigManager()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var configDir = Path.Combine(appDir, "Config");
        _keymapConfigFilePath = Path.Combine(configDir, "keymap.toml");

        Load();
    }

    public void Load()
    {
        EnsureConfigDirectoryExists();
        LoadShortcuts();
    }

    private void LoadShortcuts()
    {
        try
        {
            if (!File.Exists(_keymapConfigFilePath))
            {
                log.Info("Keymap file not found, creating empty override file: {0}", _keymapConfigFilePath);
                Shortcuts = new ShortcutsSettings();
                SaveShortcuts();
            }
            else
            {
                var tomlContent = File.ReadAllText(_keymapConfigFilePath);
                Shortcuts = TomlSerializer.Deserialize<ShortcutsSettings>(tomlContent, (TomlSerializerOptions?)null)
                    ?? new ShortcutsSettings();
                log.Info("Shortcuts loaded from {0} ({1} user overrides)", _keymapConfigFilePath, Shortcuts.Bindings.Count);
            }

            Shortcuts.RebuildNormalized(ShortcutsSettings.CurrentStyle);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading shortcuts, falling back to defaults");
            Shortcuts = new ShortcutsSettings();
            Shortcuts.RebuildNormalized(ShortcutsSettings.CurrentStyle);
        }
    }

    public void SaveShortcuts()
    {
        try
        {
            EnsureConfigDirectoryExists();
            var tomlString = TomlSerializer.Serialize(Shortcuts, (TomlSerializerOptions?)null);
            File.WriteAllText(_keymapConfigFilePath, tomlString);
            log.Info("Shortcuts saved to {0} ({1} user overrides)", _keymapConfigFilePath, Shortcuts.Bindings.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving shortcuts");
        }
    }

    /// <summary>
    /// Clears all user shortcut customizations and persists the (now empty) override file, falling
    /// back entirely to the hardcoded ShortcutsSettings.CurrentStyle defaults.
    /// </summary>
    public void RestoreDefaultShortcuts()
    {
        Shortcuts.RestoreDefaults();
        Shortcuts.RebuildNormalized(ShortcutsSettings.CurrentStyle);
        SaveShortcuts();
    }

    private void EnsureConfigDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_keymapConfigFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            log.Info("Created config directory: {0}", directory);
        }
    }
}
