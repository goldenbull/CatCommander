using System;
using System.IO;
using NLog;
using Tomlyn;

namespace CatCommander.Configuration;

public class ConfigManager
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    #region singleton

    private static ConfigManager? _instance;

    public static ConfigManager Instance
    {
        get
        {
            _instance ??= new ConfigManager();
            return _instance;
        }
    }

    #endregion

    private string ConfigFilePath { get; }
    private string PanelsFilePath { get; }
    private string KeymapFilePath { get; }

    public ApplicationSettings Application { get; private set; } = new();
    public ShortcutsSettings Shortcuts { get; private set; } = new();
    public PanelSettings[] PanelSettings { get; private set; } = [];

    private ConfigManager()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(appDir, "data");

        ConfigFilePath = Path.Combine(dataDir, "app.toml");
        PanelsFilePath = Path.Combine(dataDir, "panels.toml");
        KeymapFilePath = Path.Combine(dataDir, "keymap.toml");

        Load();
    }

    public void Load()
    {
        try
        {
            EnsureDataDirectoryExists();

            // Load each configuration component separately
            LoadApplicationSettings();
            LoadPanelSettings();
            LoadShortcuts();
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading configuration");
            // Use defaults if loading fails
            Application = new ApplicationSettings();
            Shortcuts = new ShortcutsSettings();
            PanelSettings = [];
        }
    }

    private void LoadApplicationSettings()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                log.Info("Config file not found, creating default: {0}", ConfigFilePath);
                Application = new ApplicationSettings();
                SaveApplicationSettings();
                return;
            }

            var tomlContent = File.ReadAllText(ConfigFilePath);
            Application = Toml.ToModel<ApplicationSettings>(tomlContent);
            log.Info("Application settings loaded from {0}", ConfigFilePath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading application settings");
            Application = new ApplicationSettings();
        }
    }

    private void LoadPanelSettings()
    {
        try
        {
            if (!File.Exists(PanelsFilePath))
            {
                log.Info("Panels file not found, creating default: {0}", PanelsFilePath);
                PanelSettings = [];
                SavePanelSettings();
                return;
            }

            var tomlContent = File.ReadAllText(PanelsFilePath);
            var panelsWrapper = Toml.ToModel<PanelsWrapper>(tomlContent);
            PanelSettings = panelsWrapper.Panels ?? [];
            log.Info("Panel settings loaded from {0} ({1} panels)", PanelsFilePath, PanelSettings.Length);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading panel settings");
            PanelSettings = [];
        }
    }

    private void LoadShortcuts()
    {
        try
        {
            if (!File.Exists(KeymapFilePath))
            {
                log.Info("Keymap file not found, creating default: {0}", KeymapFilePath);
                Shortcuts = new ShortcutsSettings();
                SaveShortcuts();
                return;
            }

            var tomlContent = File.ReadAllText(KeymapFilePath);
            Shortcuts = Toml.ToModel<ShortcutsSettings>(tomlContent);

            // Rebuild the reverse map (keystroke -> operation) for runtime lookups
            Shortcuts.RebuildReverseMap();

            log.Info("Shortcuts loaded from {0} ({1} operations)", KeymapFilePath, Shortcuts.MapOpToKey.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading shortcuts");
            Shortcuts = new ShortcutsSettings();
        }
    }

    public void Save()
    {
        SaveApplicationSettings();
        SavePanelSettings();
        SaveShortcuts();
    }

    public void SaveApplicationSettings()
    {
        try
        {
            EnsureDataDirectoryExists();
            var tomlString = Toml.FromModel(Application);
            File.WriteAllText(ConfigFilePath, tomlString);
            log.Info("Application settings saved to {0}", ConfigFilePath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving application settings");
        }
    }

    public void SavePanelSettings()
    {
        try
        {
            EnsureDataDirectoryExists();
            var panelsWrapper = new PanelsWrapper { Panels = PanelSettings };
            var tomlString = Toml.FromModel(panelsWrapper);
            File.WriteAllText(PanelsFilePath, tomlString);
            log.Info("Panel settings saved to {0} ({1} panels)", PanelsFilePath, PanelSettings.Length);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving panel settings");
        }
    }

    public void SaveShortcuts()
    {
        try
        {
            EnsureDataDirectoryExists();
            var tomlString = Toml.FromModel(Shortcuts);
            File.WriteAllText(KeymapFilePath, tomlString);
            log.Info("Shortcuts saved to {0} ({1} bindings)", KeymapFilePath, Shortcuts.MapOpToKey.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving shortcuts");
        }
    }

    private void EnsureDataDirectoryExists()
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            log.Info("Created data directory: {0}", directory);
        }
    }

    // Helper class for TOML serialization of panel array
    private class PanelsWrapper
    {
        public PanelSettings[]? Panels { get; set; }
    }
}