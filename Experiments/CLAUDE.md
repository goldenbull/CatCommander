# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is an experimental Avalonia UI project for testing and prototyping features. It's a simple .NET 10.0 desktop application using Avalonia 11.3.9 with MVVM architecture and Metalama.Patterns.Observability for automatic property change notifications.

## Build and Run Commands

```bash
# Build the project
dotnet build Experiments.csproj

# Run the application
dotnet run --project Experiments.csproj

# Build for release
dotnet build -c Release
```

## Architecture

This is a minimal Avalonia MVVM application demonstrating:

**Entry Point**: `Program.cs` configures the Avalonia app with platform detection, Inter font, and trace logging.

**Application Initialization**: `App.axaml.cs:OnFrameworkInitializationCompleted` creates the MainWindow.

**Main Window**: `MainWindow.axaml` contains a simple UI with buttons and a list to demonstrate data binding and command handling.

**ViewModel**: `MyViewModel` demonstrates:
- Metalama `[Observable]` attribute for automatic INotifyPropertyChanged implementation
- Simple command methods (`ClickMe`, `RunBackground`) bound directly to buttons
- ObservableCollection for list updates
- Background task execution pattern

**Logging**: NLog configured via `NLog.config` with console and file outputs. Logs are stored in `ApplicationData/CatCommander/logs/`.

## Key Dependencies

- **Avalonia 11.3.9**: Core UI framework with Desktop, Fluent theme, Inter fonts
- **Avalonia.ReactiveUI 11.3.8**: MVVM infrastructure and command handling
- **Avalonia.Controls.TreeDataGrid 11.1.1**: Data grid control
- **Metalama.Patterns.Observability 2025.1.16**: Compile-time MVVM code generation
- **NLog 6.0.6**: Logging framework

## Development Notes

### Metalama Observable Pattern

The `[Observable]` attribute automatically implements INotifyPropertyChanged:
- All public properties automatically raise PropertyChanged events
- No need for manual property backing fields or RaisePropertyChanged calls
- Works with auto-properties and collection properties

### Command Binding

Commands are bound directly to methods in XAML using `{Binding MethodName}`:
```xml
<Button Command="{Binding ClickMe}">Add 1</Button>
```

This requires Avalonia.ReactiveUI which provides automatic command wrapping.

### Background Operations

The `RunBackground` method demonstrates running operations on background threads while updating UI-bound properties. Note that property updates automatically marshal to the UI thread when using the Observable pattern.
