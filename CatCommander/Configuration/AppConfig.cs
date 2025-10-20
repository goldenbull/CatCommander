using System.Collections.Generic;

namespace CatCommander.Configuration
{
    public class AppConfig
    {
        public ApplicationSettings? Application { get; set; }
        public UiSettings? Ui { get; set; }
        public BehaviorSettings? Behavior { get; set; }
        public ShortcutsSettings? Shortcuts { get; set; }
        public LoggingSettings? Logging { get; set; }

        public AppConfig()
        {
            Application = new ApplicationSettings();
            Ui = new UiSettings();
            Behavior = new BehaviorSettings();
            Shortcuts = new ShortcutsSettings();
            Logging = new LoggingSettings();
        }
    }

    public class ApplicationSettings
    {
        public string Title { get; set; } = "CatCommander";
        public int Window_Width { get; set; } = 1200;
        public int Window_Height { get; set; } = 800;
    }

    public class UiSettings
    {
        public string Theme { get; set; } = "dark";
        public int Font_Size { get; set; } = 12;
        public bool Show_Hidden { get; set; } = false;
        public bool Dual_Pane { get; set; } = true;
    }

    public class BehaviorSettings
    {
        public string Default_Sort { get; set; } = "name";
        public bool Sort_Ascending { get; set; } = true;
        public bool Follow_Symlinks { get; set; } = true;
        public bool Confirm_Delete { get; set; } = true;
        public bool Confirm_Overwrite { get; set; } = true;
    }

    public class ShortcutsSettings
    {
        public Dictionary<Shortcuts, string> Bindings { get; } = new();

        // Helper method to get all shortcut keys for an operation
        // Returns array of shortcuts (semicolon-separated alternatives)
        // Each shortcut may contain multiple keystrokes (comma-separated sequence)
        public string[] GetShortcuts(Shortcuts operation)
        {
            if (!Bindings.TryGetValue(operation, out var keys))
            {
                return System.Array.Empty<string>();
            }

            return keys.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        }

        // Helper method to get keystroke sequences for a shortcut
        // Example: "Ctrl+K,Ctrl+C" returns ["Ctrl+K", "Ctrl+C"]
        public string[] GetKeystrokeSequence(string shortcut)
        {
            return shortcut.Split(',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
        }

        // Helper method to get all keystroke sequences for an operation
        // Returns array of arrays - each inner array is a keystroke sequence
        public string[][] GetAllKeystrokeSequences(Shortcuts operation)
        {
            var shortcuts = GetShortcuts(operation);
            var result = new string[shortcuts.Length][];
            for (int i = 0; i < shortcuts.Length; i++)
            {
                result[i] = GetKeystrokeSequence(shortcuts[i]);
            }
            return result;
        }

        // Helper method to get shortcut key(s) for an operation as raw string
        // Returns the string exactly as stored (may contain semicolons and commas)
        public string? GetShortcut(Shortcuts operation)
        {
            return Bindings.TryGetValue(operation, out var key) ? key : null;
        }

        // Helper method to check if a key/sequence triggers an operation
        // Supports both single keys and comma-separated sequences
        public bool HasKey(Shortcuts operation, string key)
        {
            var shortcuts = GetShortcuts(operation);
            return System.Array.Exists(shortcuts, s => s.Equals(key, System.StringComparison.OrdinalIgnoreCase));
        }

        // Helper method to find operation by key or keystroke sequence
        // Supports both single keys and comma-separated sequences (e.g., "Ctrl+K,Ctrl+C")
        public Shortcuts? GetOperation(string keyOrSequence)
        {
            foreach (var kvp in Bindings)
            {
                var shortcuts = kvp.Value.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (System.Array.Exists(shortcuts, k => k.Equals(keyOrSequence, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return kvp.Key;
                }
            }
            return null;
        }

        // Helper method to add a shortcut to an operation (preserves existing shortcuts)
        // The key can be a single key or comma-separated keystroke sequence
        public void AddKey(Shortcuts operation, string key)
        {
            if (Bindings.TryGetValue(operation, out var existing))
            {
                var keys = existing.Split(';', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries);
                if (!System.Array.Exists(keys, k => k.Equals(key, System.StringComparison.OrdinalIgnoreCase)))
                {
                    Bindings[operation] = existing + ";" + key;
                }
            }
            else
            {
                Bindings[operation] = key;
            }
        }

        // Helper method to set shortcuts for an operation (replaces existing)
        // Each key can be a single key or comma-separated keystroke sequence
        public void SetKeys(Shortcuts operation, params string[] keys)
        {
            Bindings[operation] = string.Join(";", keys);
        }
    }

    public class LoggingSettings
    {
        public string Level { get; set; } = "info";
        public string Log_File { get; set; } = "logs/catcommander.log";
    }
}


