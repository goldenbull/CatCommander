using System;
using System.IO;
using NLog;
using Tomlyn;
using CatCommander.Platform;

namespace CatCommander.Config;

/// <summary>
/// Loads/saves the unified config.toml plus volatile session.toml.
/// </summary>
public class ConfigManager
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly string _configFilePath;
    private readonly string _legacyKeymapFilePath;
    private readonly string _sessionFilePath;
    private readonly KeyboardStyle _keyboardStyle;

    public ApplicationSettings Settings { get; private set; } = new();
    public ShortcutsSettings Shortcuts => Settings.Shortcuts;

    public ConfigManager(string? configDirectory = null, PlatformInfo? platform = null)
    {
        _keyboardStyle = ShortcutsSettings.ForPlatform(platform ?? PlatformInfo.Current);
        var configDir = configDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CatCommander");
        _configFilePath = Path.Combine(configDir, "config.toml");
        _legacyKeymapFilePath = configDirectory is null
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "keymap.toml")
            : Path.Combine(configDir, "keymap.toml");
        _sessionFilePath = Path.Combine(configDir, "session.toml");

        Load();
    }

    public void Load()
    {
        EnsureConfigDirectoryExists();
        LoadSettings();
    }

    private void LoadSettings()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                Settings = TomlSerializer.Deserialize<ApplicationSettings>(
                    File.ReadAllText(_configFilePath), (TomlSerializerOptions?)null) ?? new();
            }
            else
            {
                Settings = new ApplicationSettings();
                // One-time compatibility migration: preserve existing user overrides, then write
                // them as the [shortcuts.bindings] section of config.toml.
                if (File.Exists(_legacyKeymapFilePath))
                {
                    Settings.Shortcuts = TomlSerializer.Deserialize<ShortcutsSettings>(
                        File.ReadAllText(_legacyKeymapFilePath), (TomlSerializerOptions?)null) ?? new();
                }
                SaveSettings();
            }

            Shortcuts.RebuildNormalized(_keyboardStyle);
            log.Info("Configuration loaded from {0} ({1} shortcut overrides)", _configFilePath, Shortcuts.Bindings.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading configuration, falling back to defaults");
            Settings = new ApplicationSettings();
            Shortcuts.RebuildNormalized(_keyboardStyle);
        }
    }

    public void SaveSettings()
    {
        try
        {
            EnsureConfigDirectoryExists();
            var tomlString = TomlSerializer.Serialize(Settings, (TomlSerializerOptions?)null);
            File.WriteAllText(_configFilePath, tomlString);
            log.Info("Configuration saved to {0}", _configFilePath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving configuration");
        }
    }

    /// <summary>
    /// Clears all user shortcut customizations and persists the (now empty) override file, falling
    /// back entirely to the hardcoded ShortcutsSettings.CurrentStyle defaults.
    /// </summary>
    public void RestoreDefaultShortcuts()
    {
        Shortcuts.RestoreDefaults();
        Shortcuts.RebuildNormalized(_keyboardStyle);
        SaveSettings();
    }

    public SessionState? LoadSession()
    {
        try
        {
            return File.Exists(_sessionFilePath)
                ? TomlSerializer.Deserialize<SessionState>(File.ReadAllText(_sessionFilePath), (TomlSerializerOptions?)null)
                : null;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading session state");
            return null;
        }
    }

    public void SaveSession(SessionState session)
    {
        try
        {
            EnsureConfigDirectoryExists();
            File.WriteAllText(_sessionFilePath, TomlSerializer.Serialize(session, (TomlSerializerOptions?)null));
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving session state");
        }
    }

    private void EnsureConfigDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            log.Info("Created config directory: {0}", directory);
        }
    }
}
