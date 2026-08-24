using System;
using System.IO;
using NLog;
using Tomlyn;

namespace CatCommander.Config;

/// <summary>
/// Loads/saves app.toml and keymap.toml. Registered in DI (not a singleton) - see App
/// composition root.
/// </summary>
public class ConfigManager
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly string _appConfigFilePath;
    private readonly string _keymapConfigFilePath;

    public ApplicationSettings Application { get; private set; } = new();
    public ShortcutsSettings Shortcuts { get; private set; } = new();

    public ConfigManager()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var configDir = Path.Combine(appDir, "Config");
        _appConfigFilePath = Path.Combine(configDir, "app.toml");
        _keymapConfigFilePath = Path.Combine(configDir, "keymap.toml");

        Load();
    }

    public void Load()
    {
        EnsureConfigDirectoryExists();
        LoadApplicationSettings();
        LoadShortcuts();
    }

    private void LoadApplicationSettings()
    {
        try
        {
            if (!File.Exists(_appConfigFilePath))
            {
                log.Info("App config file not found, creating default: {0}", _appConfigFilePath);
                Application = new ApplicationSettings();
                SaveApplicationSettings();
                return;
            }

            var tomlContent = File.ReadAllText(_appConfigFilePath);
            Application = TomlSerializer.Deserialize<ApplicationSettings>(tomlContent, (TomlSerializerOptions?)null)
                ?? new ApplicationSettings();
            log.Info("Application settings loaded from {0} (KeyboardStyle={1})", _appConfigFilePath, Application.KeyboardStyle);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading application settings, falling back to defaults");
            Application = new ApplicationSettings();
        }
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

            Shortcuts.RebuildNormalized(Application.KeyboardStyle);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading shortcuts, falling back to defaults");
            Shortcuts = new ShortcutsSettings();
            Shortcuts.RebuildNormalized(Application.KeyboardStyle);
        }
    }

    public void SaveApplicationSettings()
    {
        try
        {
            EnsureConfigDirectoryExists();
            var tomlString = TomlSerializer.Serialize(Application, (TomlSerializerOptions?)null);
            File.WriteAllText(_appConfigFilePath, tomlString);
            log.Info("Application settings saved to {0}", _appConfigFilePath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving application settings");
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
    /// Switches which built-in default set fills gaps in the user's shortcut overrides. Doesn't
    /// touch the user's actual customizations (Bindings) - only which defaults resolve underneath
    /// them.
    /// </summary>
    public void SetKeyboardStyle(KeyboardStyle style)
    {
        Application.KeyboardStyle = style;
        SaveApplicationSettings();
        Shortcuts.RebuildNormalized(Application.KeyboardStyle);
    }

    /// <summary>
    /// Clears all user shortcut customizations and persists the (now empty) override file.
    /// </summary>
    public void RestoreDefaultShortcuts()
    {
        Shortcuts.RestoreDefaults();
        Shortcuts.RebuildNormalized(Application.KeyboardStyle);
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
