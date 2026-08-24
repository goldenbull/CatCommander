# Configuration System

CatCommander now includes a TOML-based configuration system for easy customization.

## Configuration File

The configuration is stored in `config.toml` which is created automatically on first run. You can manually edit this file to customize the application.

## Configuration Sections

### Application
- `title`: Application window title
- `window_width`: Default window width
- `window_height`: Default window height

### UI
- `theme`: Color theme ("light" or "dark")
- `font_size`: Font size for file list
- `show_hidden`: Show hidden files by default
- `dual_pane`: Enable dual pane mode

### Behavior
- `default_sort`: Default sort order ("name", "size", "date", "extension")
- `sort_ascending`: Sort in ascending order by default
- `follow_symlinks`: Follow symbolic links
- `confirm_delete`: Ask for confirmation before deleting
- `confirm_overwrite`: Ask for confirmation before overwriting

### Shortcuts
Keyboard shortcuts are defined as a mapping from operations (enum) to key combinations. Available operations:
- `Copy`: F5, Ctrl+C - Copy files/folders
- `Move`: F6, Ctrl+X - Move files/folders
- `Rename`: Shift+F6, F2 - Rename file/folder
- `Delete`: F8, Delete - Delete files/folders
- `ExpandCurrentFolder`: Right - Expand current folder in tree
- `ExpandSelectedFolders`: Ctrl+Right - Expand selected folders
- `GoIntoCurrentFolder`: Enter, Ctrl+Down - Navigate into current folder
- `GoBackToParentFolder`: Backspace, Ctrl+Up - Go to parent folder
- `GotoFirstItem`: Home, Ctrl+Home - Jump to first item
- `GotoLastItem`: End, Ctrl+End - Jump to last item

**Shortcut Format**:
- **Semicolons (`;`)** separate alternative shortcuts for the same operation
- **Commas (`,`)** separate keystrokes in a sequence (for multi-keystroke chords, like Vim or VS Code style)

**Examples**:
```toml
# Single key
Copy = "F5"

# Multiple alternative shortcuts
Copy = "F5;Ctrl+C"

# Keystroke sequence (chord)
Comment = "Ctrl+K,Ctrl+C"

# Combined: multiple alternatives, some with sequences
Copy = "F5;Ctrl+C;Ctrl+K,Ctrl+Y"
```

In the TOML file, shortcuts are defined under `[shortcuts.bindings]` section.

### Logging
- `level`: Log level ("trace", "debug", "info", "warn", "error")
- `log_file`: Path to log file

## Usage in Code

```csharp
// Access configuration
using CatCommander.Configuration;

var config = ConfigManager.Instance.Config;

// Read settings
string theme = config.Ui.Theme;
int fontSize = config.Ui.Font_Size;
bool showHidden = config.Ui.Show_Hidden;

// Modify settings
config.Ui.Theme = "light";
config.Ui.Font_Size = 14;

// Access shortcuts - get raw string (may contain ; and ,)
string? copyKeys = config.Shortcuts.GetShortcut(Shortcuts.Copy);
// Returns "F5;Ctrl+C"

// Get array of alternative shortcuts
string[] copyArray = config.Shortcuts.GetShortcuts(Shortcuts.Copy);
// Returns ["F5", "Ctrl+C"]

// Parse a keystroke sequence (comma-separated)
string[] sequence = config.Shortcuts.GetKeystrokeSequence("Ctrl+K,Ctrl+C");
// Returns ["Ctrl+K", "Ctrl+C"]

// Get all keystroke sequences for an operation (handles both ; and ,)
string[][] allSequences = config.Shortcuts.GetAllKeystrokeSequences(Shortcuts.Copy);
// Returns [["F5"], ["Ctrl+C"]] for "F5;Ctrl+C"
// Returns [["Ctrl+K", "Ctrl+C"], ["F5"]] for "Ctrl+K,Ctrl+C;F5"

// Find operation by key or sequence
Shortcuts? op1 = config.Shortcuts.GetOperation("F8");              // Returns Shortcuts.Delete
Shortcuts? op2 = config.Shortcuts.GetOperation("Delete");          // Also returns Shortcuts.Delete
Shortcuts? op3 = config.Shortcuts.GetOperation("Ctrl+K,Ctrl+C");   // Looks for exact sequence match

// Check if a specific key/sequence is bound to an operation
bool hasKey1 = config.Shortcuts.HasKey(Shortcuts.Copy, "F5");           // true
bool hasKey2 = config.Shortcuts.HasKey(Shortcuts.Copy, "Ctrl+K,Ctrl+C"); // true if defined

// Modify shortcuts - replace all
config.Shortcuts.Bindings[Shortcuts.Copy] = "Ctrl+C";                   // Single key
config.Shortcuts.Bindings[Shortcuts.Copy] = "F5;Ctrl+C";                // Multiple alternatives
config.Shortcuts.Bindings[Shortcuts.Copy] = "Ctrl+K,Ctrl+C";            // Keystroke sequence
config.Shortcuts.Bindings[Shortcuts.Copy] = "F5;Ctrl+K,Ctrl+C";         // Combined

// Or use helper methods
config.Shortcuts.SetKeys(Shortcuts.Copy, "F5", "Ctrl+C", "Ctrl+K,Ctrl+Y"); // Set alternatives
config.Shortcuts.AddKey(Shortcuts.Copy, "Shift+F5");                       // Add one more

// Save changes
ConfigManager.Instance.Save();

// Reload from file
ConfigManager.Instance.Reload();
```

## File Location

The config file is located in the application's base directory:
- Windows: Same directory as the executable
- macOS: Same directory as the .app bundle executable
- Linux: Same directory as the executable

If the config file doesn't exist, it will be created with default values on first run.
