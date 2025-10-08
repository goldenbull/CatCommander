# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

CatCommander is a cross-platform mouse-free file manager written in C# using Avalonia UI, inspired by Total Commander. The project is built on .NET 9.0 and uses the Avalonia 11.3.6 framework for cross-platform desktop UI.

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

**Entry Point**: `Program.cs` configures the Avalonia app and starts the desktop lifetime.

**Application Structure**: Standard Avalonia MVVM pattern:
- `App.axaml` / `App.axaml.cs` - Application-level resources and initialization
- `MainWindow.axaml` / `MainWindow.axaml.cs` - Main window definition and code-behind
- XAML files (`.axaml`) define UI, code-behind (`.axaml.cs`) handles logic

**Key Configuration**:
- Compiled bindings enabled by default (`AvaloniaUseCompiledBindingsByDefault`)
- Fluent theme applied
- Inter font family included

## Development Notes

The project currently has minimal implementation - just a basic window displaying the project's purpose. Future development will involve building out the dual-pane file manager interface and keyboard-driven navigation system typical of orthodox file managers like Total Commander.
