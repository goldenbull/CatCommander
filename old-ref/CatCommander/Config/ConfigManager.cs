using System;
using System.IO;
using NLog;
using Tomlyn;

namespace CatCommander.Config;

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

    private string AppConfigFilePath { get; }
    private string PanelsConfigFilePath { get; }
    private string KeymapConfigFilePath { get; }

    public ApplicationSettings Application { get; private set; } = new();
    public ShortcutsSettings Shortcuts { get; private set; } = new();
    public PanelSettings[] PanelSettings { get; private set; } = [];

    private ConfigManager()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        var configDir = Path.Combine(appDir, "Config");

        AppConfigFilePath = Path.Combine(configDir, "app.toml");
        KeymapConfigFilePath = Path.Combine(configDir, "keymap.toml");
        PanelsConfigFilePath = Path.Combine(configDir, "panels.toml");

        Load();
    }

    public void Load()
    {
        try
        {
            EnsureDataDirectoryExists();

            // Load each configuration component separately
            LoadApplicationSettings();
            LoadShortcuts();
            LoadPanelSettings();
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

    private void EnsureDataDirectoryExists()
    {
        var directory = Path.GetDirectoryName(AppConfigFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            log.Info("Created config directory: {0}", directory);
        }
    }

    private void LoadApplicationSettings()
    {
        try
        {
            if (!File.Exists(AppConfigFilePath))
            {
                log.Info("Config file not found, creating default: {0}", AppConfigFilePath);
                Application = new ApplicationSettings();
                SaveApplicationSettings();
                return;
            }

            var tomlContent = File.ReadAllText(AppConfigFilePath);
            Application = Toml.ToModel<ApplicationSettings>(tomlContent);
            log.Info("Application settings loaded from {0}", AppConfigFilePath);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading application settings");
            Application = new ApplicationSettings();
        }
    }

    private void LoadShortcuts()
    {
        try
        {
            if (!File.Exists(KeymapConfigFilePath))
            {
                log.Info("Keymap file not found, creating default: {0}", KeymapConfigFilePath);
                Shortcuts = new ShortcutsSettings();
                SaveShortcuts();
                return;
            }

            var tomlContent = File.ReadAllText(KeymapConfigFilePath);
            Shortcuts = Toml.ToModel<ShortcutsSettings>(tomlContent);

            // Rebuild the reverse map (keystroke -> operation) for runtime lookups
            Shortcuts.RebuildNormalized();

            log.Info("Shortcuts loaded from {0} ({1} operations)", KeymapConfigFilePath, Shortcuts.Bindings.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading shortcuts");
            Shortcuts = new ShortcutsSettings();
        }
    }

    private void LoadPanelSettings()
    {
        try
        {
            if (!File.Exists(PanelsConfigFilePath))
            {
                log.Info("Panels file not found, creating default: {0}", PanelsConfigFilePath);
                PanelSettings = [];
                SavePanelSettings();
                return;
            }

            var tomlContent = File.ReadAllText(PanelsConfigFilePath);
            var panelsWrapper = Toml.ToModel<PanelsWrapper>(tomlContent);
            PanelSettings = panelsWrapper.Panels ?? [];
            log.Info("Panel settings loaded from {0} ({1} panels)", PanelsConfigFilePath, PanelSettings.Length);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error loading panel settings");
            PanelSettings = [];
        }
    }

    public void Save()
    {
        SaveApplicationSettings();
        SaveShortcuts();
        SavePanelSettings();
    }

    public void SaveApplicationSettings()
    {
        try
        {
            EnsureDataDirectoryExists();
            var tomlString = Toml.FromModel(Application);
            File.WriteAllText(AppConfigFilePath, tomlString);
            log.Info("Application settings saved to {0}", AppConfigFilePath);
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
            EnsureDataDirectoryExists();
            var tomlString = Toml.FromModel(Shortcuts);
            File.WriteAllText(KeymapConfigFilePath, tomlString);
            log.Info("Shortcuts saved to {0} ({1} bindings)", KeymapConfigFilePath, Shortcuts.Bindings.Count);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving shortcuts");
        }
    }

    public void SavePanelSettings()
    {
        try
        {
            EnsureDataDirectoryExists();
            var panelsWrapper = new PanelsWrapper { Panels = PanelSettings };
            var tomlString = Toml.FromModel(panelsWrapper);
            File.WriteAllText(PanelsConfigFilePath, tomlString);
            log.Info("Panel settings saved to {0} ({1} panels)", PanelsConfigFilePath, PanelSettings.Length);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Error saving panel settings");
        }
    }

    // Helper class for TOML serialization of panel array
    private class PanelsWrapper
    {
        public PanelSettings[]? Panels { get; set; }
    }
}