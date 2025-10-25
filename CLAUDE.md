# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CatCommander is a cross-platform mouse-free file manager written in C# using Avalonia UI, inspired by Total Commander. The project is built on .NET 9.0 and uses Avalonia 11.3.7 framework for cross-platform desktop UI.

## Build and Run Commands

```bash
# Build the entire solution
dotnet build

# Build a specific project
dotnet build CatCommander/CatCommander.csproj
dotnet build libcat/libcat.csproj

# Run the application
dotnet run --project CatCommander/CatCommander.csproj

# Run tests
dotnet test

# Run tests for specific project
dotnet test TestCat/TestCat.csproj

# Build for release
dotnet build -c Release
```

## Architecture

### Solution Structure

The solution consists of three projects:

1. **CatCommander** (main UI application): Avalonia-based desktop application with MVVM architecture
2. **libcat** (shared library): Platform-independent data models and utilities for file system operations, archive handling, and system integration
3. **TestCat** (test project): xUnit-based test suite for libcat functionality

### Architectural Layers

CatCommander follows a standard Avalonia MVVM architecture with three main conceptual layers:

1. **Data source layer** (libcat): Read files from file system, compressed archives, SFTP, FTP, etc.
2. **UI layer** (CatCommander): Dual-pane file browser interface
3. **Command management layer** (CatCommander): Centralized command handling via `CommandManager` that bridges keyboard/mouse events to file operations

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
- `MainWindowViewModel`: Root view model for the main window
- `MainPanelViewModel`: Manages individual panel state (left/right panes)
- `ItemsBrowserViewModel`: Manages file system browsing, directory loading, and path history

**Command Management**:
- `CommandManager`: Centralized command coordinator that owns all ReactiveCommands (Open, Copy, Move, Rename, Delete, navigation, etc.)
  - Created with reference to `MainWindowViewModel`
  - All commands follow pattern: Execute method, CanExecute method, and CanExecute observable
  - Commands are currently stubs awaiting implementation

**Data Models (libcat project)**:
- `IFileSystemItem`: Interface for file system items from various sources (disk, zip, SFTP, FTP)
  - Properties: Name, FullPath, Extension, Size, timestamps, permissions, ItemType, DisplaySize, DisplayIcon
  - `FileSystemItemType` enum: File, Directory, SymbolicLink, Special
- `FileItemModel`: Concrete implementation of IFileSystemItem for local file system
- `FileItemTreeNode`: Tree structure for hierarchical file browsing

**Utilities (libcat project)**:
- `FileSystemHelper`: Cross-platform file system operations
  - Drive enumeration (all, local, removable, network, optical)
  - Special locations (Home, Desktop, Documents, Downloads, etc.)
  - Platform detection (Windows, macOS, Linux)
  - File size formatting
  - System icon retrieval (platform-specific)
- `SystemIconProvider`: Platform-specific icon retrieval for files/folders
- `ArchiveFileHelper`: Archive file operations using SharpCompress (view, compress, decompress)

**Configuration System**: TOML-based configuration loaded from `config.toml`:
- `ConfigManager`: Singleton that loads/saves configuration using Tomlyn library
- `AppConfig`: Structured configuration with sections for Application, UI, Behavior, Shortcuts, and Logging
- `ShortcutsSettings`: Supports complex keybindings with alternatives (semicolon-separated) and multi-keystroke sequences (comma-separated)
  - Example: `Copy = "F5;Ctrl+C"` (two alternatives)
  - Example: `Copy = "Ctrl+K,Ctrl+C"` (keystroke sequence)
- `Shortcuts` enum defines available operations

**Logging**: NLog configured via `NLog.config`, injected throughout the application (logger instances in ViewModels and MainWindow)

### Key Dependencies

**CatCommander (UI project)**:
- **Avalonia 11.3.7**: Core UI framework with Desktop, Fluent theme, Inter fonts
- **Avalonia.Controls.TreeDataGrid**: File list display
- **Semi.Avalonia**: Theme and extended controls (AvaloniaEdit, ColorPicker, DataGrid, Dock, TreeDataGrid)
- **ReactiveUI 22.1.1**: Command handling and reactive patterns (MVVM infrastructure)
- **Tomlyn 0.19.0**: TOML configuration parsing
- **NLog 6.0.5**: Logging framework with Microsoft.Extensions.Logging integration

**libcat (data/utilities library)**:
- **Avalonia 11.3.7**: For platform-independent types
- **SharpCompress 0.41.0**: Archive file support (ZIP, RAR, 7z, tar, etc.)
- **NLog 6.0.5**: Logging
- **System.Drawing.Common 9.0.0**: Icon extraction and image handling

**TestCat (test project)**:
- **xUnit 2.9.2**: Test framework
- **Microsoft.NET.Test.Sdk 17.12.0**: Test infrastructure
- **coverlet.collector 6.0.2**: Code coverage

### Project Structure

```
/
├── CatCommander/              # Main UI application
│   ├── Commands/              # Command management
│   │   └── CommandManager.cs  # Centralized command coordinator
│   ├── Configuration/         # TOML configuration management
│   │   ├── ConfigManager.cs   # Singleton loader/saver
│   │   ├── AppConfig.cs       # Configuration data structures
│   │   └── Shortcuts.cs       # Keyboard shortcuts enum
│   ├── ViewModels/            # MVVM view models
│   │   ├── MainWindowViewModel.cs
│   │   ├── MainPanelViewModel.cs
│   │   └── ItemsBrowserViewModel.cs
│   ├── View/                  # Reusable UI controls
│   │   ├── ItemsBrowser.axaml/.cs
│   │   └── MainPanel.axaml/.cs
│   ├── Images/                # PNG icons for toolbar/menus
│   ├── Program.cs             # Application entry point
│   ├── App.axaml/.cs          # Application resources and initialization
│   ├── MainWindow.axaml/.cs   # Main dual-pane window
│   ├── config.toml            # User configuration file
│   └── NLog.config            # Logging configuration
│
├── libcat/                    # Shared library (data + utilities)
│   ├── Models/                # Data models
│   │   ├── IFileSystemItem.cs # Interface for file system items
│   │   └── FileItemModel.cs   # Local file system implementation
│   └── Utils/                 # Cross-platform utilities
│       ├── FileSystemHelper.cs       # Drive/location enumeration
│       ├── SystemIconProvider.cs     # Platform-specific icons
│       ├── ArchiveFileHelper.cs      # Archive operations
│       └── FileItemTreeNode.cs       # Tree structure for files
│
└── TestCat/                   # Test project
    └── UnitTest1.cs           # xUnit tests
```

## Development Notes

### Project References

- **CatCommander** depends on **libcat** (for data models and utilities)
- **TestCat** depends on **libcat** (tests library functionality)
- Add new platform-independent utilities to **libcat**
- Add new UI components and ViewModels to **CatCommander**

### Command Pattern

All user actions flow through `CommandManager`:
1. Commands are ReactiveCommands created in `CommandManager` constructor
2. Each command has three parts:
   - `Execute{CommandName}()`: Implementation (currently stubs)
   - `CanExecute{CommandName}()`: Boolean check if command can run
   - `CanExecute{CommandName}Observable`: Observable wrapper for ReactiveUI
3. Commands are bound to keyboard shortcuts via configuration and to UI elements

To add a new command:
1. Add command property to `CommandManager`
2. Initialize in `InitializeCommands()`
3. Create Execute/CanExecute methods and observable
4. Wire up in UI (MainWindow.axaml or keybindings)

### Configuration System

The keymap configuration supports flexible keyboard shortcuts:
- **Alternative shortcuts**: Separate with semicolons (`;`) - e.g., `"F5;Ctrl+C"`
- **Keystroke sequences**: Separate with commas (`,`) - e.g., `"Ctrl+K,Ctrl+C"` for Vim-style chords
- Helper methods in `ShortcutsSettings` parse these formats

### Platform-Specific Behavior

The UI adapts based on platform:
- macOS: Uses native menu bar via `NativeMenuBar` and `NativeMenu.Menu`
- Windows/Linux: Uses Avalonia menu controls with fallback styling

File system operations in `FileSystemHelper` provide platform-specific implementations for:
- System icon retrieval (Windows SHGetFileInfo approach, macOS UTI identifiers, Linux MIME types)
- Special folder locations (different paths for each OS)
- Drive enumeration (handles differences in drive/volume representation)

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
