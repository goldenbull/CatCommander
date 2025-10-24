# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CatCommander is a cross-platform mouse-free file manager written in C# using Avalonia UI, inspired by Total Commander. The project is built on .NET 9.0 and uses Avalonia 11.3.7 framework for cross-platform desktop UI.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run --project CatCommander/CatCommander.csproj

# Build for release
dotnet build -c Release
```

## Architecture

CatCommander follows a standard Avalonia MVVM architecture with three main conceptual layers described in the README:

1. **Data source layer**: Read files from file system, compressed archives, SFTP, FTP, etc.
2. **UI layer**: Dual-pane file browser interface
3. **Keymap management layer**: Combining key events and commands

### Key Components

**Entry Point**: `Program.cs` configures the Avalonia app with platform detection, Inter font, and logging.

**Application Initialization**: `App.axaml.cs:OnFrameworkInitializationCompleted` loads configuration via `ConfigManager` singleton and creates the main window. Configuration is saved on application exit.

**Main Window**: `MainWindow.axaml` defines the dual-pane layout with:
- Platform-specific native menu bar (macOS uses native menu, Windows/Linux use fallback)
- Toolbar with navigation buttons and icons
- Two `MainPanel` instances (left/right panes) separated by grid splitters and a vertical button menu
- Each `MainPanel` contains an `ItemsBrowser` control

**File Browser**: `ItemsBrowser` (UI component) paired with `ItemsBrowserViewModel`:
- Displays files/folders in a TreeDataGrid with columns: Name, Extension, Size, Modified
- AutoCompleteBox for path navigation with history dropdown
- Loads directory contents on path change
- Maintains path history (last 20 paths)

**View Models**:
- `MainViewModel`: Contains ReactiveCommand definitions for file operations (Copy, Move, Rename, Delete) and navigation commands (currently stubs)
- `ItemsBrowserViewModel`: Manages file system browsing, directory loading, and path history

**Models**:
- `FileItemModel`: Represents a file or directory with properties (Name, FullPath, Size, Modified, IsDirectory, Extension) and formatted display size

**Configuration System**: TOML-based configuration loaded from `config.toml`:
- `ConfigManager`: Singleton that loads/saves configuration using Tomlyn library
- `AppConfig`: Structured configuration with sections for Application, UI, Behavior, Shortcuts, and Logging
- `ShortcutsSettings`: Supports complex keybindings with alternatives (semicolon-separated) and multi-keystroke sequences (comma-separated)
  - Example: `Copy = "F5;Ctrl+C"` (two alternatives)
  - Example: `Copy = "Ctrl+K,Ctrl+C"` (keystroke sequence)
- `Shortcuts` enum defines available operations

**Logging**: NLog configured via `NLog.config`, injected throughout the application (logger instances in ViewModels and MainWindow)

### Key Dependencies

- **Avalonia 11.3.7**: Core UI framework with Desktop, Fluent theme, Inter fonts
- **Avalonia.Controls.TreeDataGrid**: File list display
- **Semi.Avalonia**: Theme and extended controls (AvaloniaEdit, ColorPicker, DataGrid, Dock, TreeDataGrid)
- **ReactiveUI 22.1.1**: Command handling and reactive patterns
- **Tomlyn 0.19.0**: TOML configuration parsing
- **SharpCompress 0.41.0**: Archive file support
- **NLog 6.0.5**: Logging framework with Microsoft.Extensions.Logging integration

### File Structure

```
CatCommander/
├── Configuration/        # TOML configuration management
│   ├── ConfigManager.cs  # Singleton loader/saver
│   ├── AppConfig.cs      # Configuration data structures
│   └── Shortcuts.cs      # Keyboard shortcuts enum
├── ViewModels/          # MVVM view models
│   ├── MainViewModel.cs
│   └── ItemsBrowserViewModel.cs
├── Models/              # Data models
│   └── FileItemModel.cs
├── UI/                  # Reusable UI controls
│   ├── ItemsBrowser.axaml/.cs
│   └── MainPanel.axaml/.cs
├── Images/              # PNG icons for toolbar/menus
├── lib/                 # Native libraries (7z.dylib, 7z.so)
├── Program.cs           # Application entry point
├── App.axaml/.cs        # Application resources and initialization
├── MainWindow.axaml/.cs # Main dual-pane window
├── config.toml          # User configuration file
└── NLog.config          # Logging configuration
```

## Development Notes

### Configuration System

The keymap configuration supports flexible keyboard shortcuts:
- **Alternative shortcuts**: Separate with semicolons (`;`) - e.g., `"F5;Ctrl+C"`
- **Keystroke sequences**: Separate with commas (`,`) - e.g., `"Ctrl+K,Ctrl+C"` for Vim-style chords
- Helper methods in `ShortcutsSettings` parse these formats

### Platform-Specific Behavior

The UI adapts based on platform:
- macOS: Uses native menu bar via `NativeMenuBar` and `NativeMenu.Menu`
- Windows/Linux: Uses Avalonia menu controls with fallback styling

### Planned Features (from README TODO)

The application is in active development with planned features including:
- Compressed file operations (view, compress, decompress)
- File preview (text, markdown, office, image, hex)
- Terminal integration (cmd/PowerShell/bash/zsh)
- Folder comparison, filtering, sorting
- Navigation history (back/forward)
- Tabs with duplication and favorites
- Fast filtering by filename
- SFTP/FTP support
- Customizable themes
