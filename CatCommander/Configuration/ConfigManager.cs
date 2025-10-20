using System;
using System.IO;
using Tomlyn;

namespace CatCommander.Configuration
{
    public class ConfigManager
    {
        private static ConfigManager? _instance;
        private static readonly object _lock = new();

        public AppConfig Config { get; private set; }
        public string ConfigFilePath { get; }

        private ConfigManager(string configFilePath)
        {
            ConfigFilePath = configFilePath;
            Config = new AppConfig();
        }

        public static ConfigManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new ConfigManager(GetDefaultConfigPath());
                    }
                }
                return _instance;
            }
        }

        private static string GetDefaultConfigPath()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, "config.toml");
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(ConfigFilePath))
                {
                    // Create default config file if it doesn't exist
                    SaveDefaults();
                    return;
                }

                var tomlContent = File.ReadAllText(ConfigFilePath);

                // Use Tomlyn's reflection-based deserialization
                Config = Toml.ToModel<AppConfig>(tomlContent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
                // Use defaults if loading fails
                Config = new AppConfig();
            }
        }

        public void Save()
        {
            try
            {
                // Use Tomlyn's reflection-based serialization
                var tomlString = Toml.FromModel(Config);

                // Ensure directory exists
                var directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(ConfigFilePath, tomlString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }

        private void SaveDefaults()
        {
            Config = new AppConfig();
            Save();
        }

        public void Reload()
        {
            Load();
        }
    }
}
