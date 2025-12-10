# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CatCommander is a cross-platform, mouse-free file manager written in C# using Avalonia UI, inspired by Total Commander. The application runs on .NET 10.0 and targets macOS (ARM64 by default), with support for Windows and Linux.

## Solution Structure

The solution contains three projects:

- **CatCommander**: Main Avalonia UI application with MVVM architecture
- **libcat**: Shared library containing file system utilities, models, and helpers
- **TestCat**: xUnit test project for libcat

## Build Commands

### Development

```bash
# Build solution
dotnet build

# Run application (development mode, generic .NET icon)
dotnet run --project CatCommander/CatCommander.csproj

# Run tests
dotnet test

# Run specific test file
dotnet test --filter "FullyQualifiedName~FileItemTreeNodeTests"
```

### Production (macOS)

```bash
# Build macOS .app bundle with custom icon (ARM64)
./build-macos-app.sh

# Run the built .app
open build/CatCommander.app
```

For Intel Macs, modify `build-macos-app.sh` to use `-r osx-x64` instead of `-r osx-arm64`.

## Architecture

### MVVM Pattern

The app follows classic MVVM architecture:

- **Views**: Avalonia XAML files in `CatCommander/View/` and root `MainWindow.axaml`
- **ViewModels**: In `CatCommander/ViewModels/`
  - `MainWindowViewModel`: Global application state
  - `MainPanelViewModel`: Panel-specific state (dual-pane model for fast UI)
  - `ItemsBrowserViewModel`: File browsing logic
- **Models**: In `libcat/Models/` (shared with views via ViewModels)

### Key System Components

1. **Configuration System** (`CatCommander/Configuration/`)
   - `ConfigManager`: Singleton that manages three separate TOML files:
     - `data/config.toml`: Application settings (theme, window size, etc.)
     - `data/panels.toml`: Panel states (paths, sort order) - restored on startup
     - `data/keymap.toml`: Keyboard shortcuts
   - `ApplicationSettings`: Global app settings (loaded from config.toml)
   - `PanelSettings`: Panel state array (loaded from panels.toml)
   - `ShortcutsSettings`: Keymap with support for:
     - Multiple alternatives (semicolon-separated: `"F5;Ctrl+C"`)
     - Keystroke sequences (comma-separated: `"Ctrl+K,Ctrl+C"`)
   - Config files location: `<executable-dir>/data/`
   - See `CONFIG.md` for detailed configuration documentation

2. **Command System** (`CatCommander/Commands/`)
   - `CommandExecutor`: Central command dispatcher using ReactiveUI commands
   - All file operations (Copy, Move, Rename, Delete, navigation) are ReactiveCommands
   - Commands are delegated from `MainWindowViewModel` to `CommandExecutor`
   - Each command has three parts:
     - `Execute*()`: Command implementation
     - `CanExecute*()`: Validation logic
     - `CanExecute*Observable`: ReactiveUI observable for binding

3. **Keyboard Hook System** (`CatCommander/Commands/KeyboardHookManager.cs`)
   - Uses SharpHook for global low-level keyboard hooks
   - Tracks modifier keys (Ctrl, Alt, Shift, Meta/Cmd)
   - Normalizes key combinations to consistent format (e.g., `"ctrl+alt+a"`)
   - Raises `KeyPressed` event with `CatKeyEventArgs` containing modifiers and key code
   - Handles multi-keystroke sequences from configuration

4. **File System Layer** (`libcat/`)
   - `FileSystemHelper`: Core file operations (navigation, read/write)
   - `ArchiveFileHelper`: Archive file browsing (7z support via `lib/7z.dylib|so`)
   - `SystemIconProvider`: Platform-specific file icons
   - `FileItemTreeNode`: Tree structure for hierarchical file browsing
   - `FileItemModel` & `IFileSystemItem`: Abstraction over files, folders, archives, and future remote sources (SFTP/FTP)

### Dependency Flow

```
UI Layer (Views/XAML)
    ↓ binds to
ViewModels (MainWindowViewModel, PanelViewModel)
    ↓ delegates commands to
CommandExecutor
    ↓ uses
FileSystemHelper / ArchiveFileHelper
    ↓ operates on
FileItemModel / FileItemTreeNode
```

## Configuration System

The app uses three TOML files for configuration, managed by `ConfigManager` singleton:
- `data/config.toml`: Application settings (accessed via `ConfigManager.Instance.Application`)
- `data/panels.toml`: Panel states (accessed via `ConfigManager.Instance.PanelSettings`)
- `data/keymap.toml`: Keyboard shortcuts (accessed via `ConfigManager.Instance.Shortcuts`)

**Important conventions**:
- Shortcuts use enums (`Shortcuts` enum in `Configuration/Shortcuts.cs`)
- Enum names match both config keys AND command names in `CommandExecutor`
- Shortcut format supports:
  - Single key: `"F5"`
  - Multiple alternatives: `"F5;Ctrl+C"` (semicolon-separated)
  - Keystroke sequences: `"Ctrl+K,Ctrl+C"` (comma-separated for chords)
  - Combined: `"F5;Ctrl+K,Ctrl+C"` (both alternatives and sequences)

**Loading and saving**:
```csharp
ConfigManager.Instance.Load();                    // Load all config files on startup
ConfigManager.Instance.Save();                    // Save all config files
ConfigManager.Instance.SaveApplicationSettings(); // Save only config.toml
ConfigManager.Instance.SavePanelSettings();       // Save only panels.toml
ConfigManager.Instance.SaveShortcuts();           // Save only keymap.toml
```

## Key Technologies

- **Avalonia 11.3.9**: Cross-platform UI framework
- **ReactiveUI 22.3.1**: MVVM with reactive commands
- **Metalama.Patterns.Observability**: Automatic INotifyPropertyChanged implementation (AOP)
- **SharpHook 7.1.0**: Low-level global keyboard hooks
- **Tomlyn 0.19.0**: TOML serialization/deserialization
- **NLog 6.0.6**: Logging framework (config in `NLog.config`)
- **SharpCompress 0.42.0**: Archive file support (7z, zip, etc.)
- **Semi.Avalonia**: UI theme library (Fluent design)

## Code Patterns

### Adding a New Command

1. Add enum value to `Shortcuts` in `Configuration/Shortcuts.cs`
2. Add default binding to `ShortcutsSettings.InitializeDefault()` method
3. Add ReactiveCommand property to `CommandExecutor.cs`
4. Implement three methods in `CommandExecutor.cs`:
   - `Execute*()`: Logic
   - `CanExecute*()`: Validation
   - `CanExecute*Observable`: Observable for bindings
5. Initialize in `InitializeCommands()`
6. Expose command as property in `MainWindowViewModel.cs`

### Working with Configuration

```csharp
// Access configuration settings directly from ConfigManager singleton
var appSettings = ConfigManager.Instance.Application;
var shortcuts = ConfigManager.Instance.Shortcuts;
var panels = ConfigManager.Instance.PanelSettings;

// Read application settings
string theme = appSettings.Theme;
int windowWidth = appSettings.Window_Width;
bool showHidden = appSettings.Show_Hidden;

// Modify application settings
appSettings.Theme = "light";
appSettings.Window_Width = 1600;
ConfigManager.Instance.SaveApplicationSettings();

// Read shortcuts
string[] copyShortcuts = shortcuts.GetShortcuts(Shortcuts.Copy);
Shortcuts? operation = shortcuts.GetOperation("F5");

// Modify shortcuts
shortcuts.SetKeys(Shortcuts.Copy, "F5", "Ctrl+C");
shortcuts.AddKey(Shortcuts.Copy, "Shift+F5");
ConfigManager.Instance.SaveShortcuts();

// Work with panel settings
foreach (var panel in panels)
{
    Console.WriteLine($"Panel: {panel.Root_Path}, Sort: {panel.Sort_Column}");
}

// Save all configuration files
ConfigManager.Instance.Save();
```

### Using Metalama Observability

The project uses Metalama for automatic property change notifications. Properties in ViewModels are automatically instrumented with INotifyPropertyChanged:

```csharp
// No need for manual OnPropertyChanged calls in most cases
public string MyProperty { get; set; }
```

## File Locations

- Config files (created on first run):
  - `<executable-dir>/data/config.toml`: Application settings
  - `<executable-dir>/data/panels.toml`: Panel states
  - `<executable-dir>/data/keymap.toml`: Keyboard shortcuts
- Logs: Configured in `NLog.config` (default: console)
- Native libraries: `lib/7z.dylib` (macOS), `lib/7z.so` (Linux)
- macOS .app bundle: `build/CatCommander.app` (after running build script)

## Development Notes

- The app uses compiled bindings by default (`AvaloniaUseCompiledBindingsByDefault=true`)
- Avalonia.Diagnostics is included in Debug builds only
- All commands currently have TODO placeholders - file operations are not fully implemented
- Configuration system is fully functional
- Keyboard hook manager is fully functional
- Platform-specific icon rendering is implemented via `SystemIconProvider`
