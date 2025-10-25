# Building CatCommander for macOS

This guide explains how to build a proper macOS `.app` bundle with the cat icon.

## Quick Start

```bash
./build-macos-app.sh
```

This will create a `CatCommander.app` bundle in the `build/` directory.

## What the Script Does

1. **Builds the project** using `dotnet publish` with Release configuration
2. **Creates .app bundle structure** following macOS conventions:
   - `CatCommander.app/Contents/MacOS/` - Contains the executable and dependencies
   - `CatCommander.app/Contents/Resources/` - Contains the icon file
   - `CatCommander.app/Contents/Info.plist` - Application metadata
3. **Adds the cat icon** (`cat.icns`) to the Resources folder
4. **Makes the app executable** with proper permissions

## Running the App

### Option 1: Double-click
Navigate to `build/CatCommander.app` in Finder and double-click to launch.

### Option 2: Command line
```bash
open build/CatCommander.app
```

### Option 3: Install to Applications
```bash
cp -R build/CatCommander.app /Applications/
```

Then launch from Launchpad or Applications folder.

## Verifying the Icon

After launching the app, you should see the 🐱 cat icon:
- In the **Dock** while the app is running
- In the **window title bar**
- In **Finder** when viewing the .app bundle
- In **Mission Control** and **Command+Tab** app switcher

## Development vs Production

- **Development**: Use `dotnet run` for quick testing (Dock icon will be generic .NET icon)
- **Production**: Use `./build-macos-app.sh` to create a proper `.app` bundle with custom icon

## Files Created

The build script uses these resources:

- `CatCommander/Assets/cat.icns` - macOS icon file (multi-resolution)
- `CatCommander/Assets/Info.plist` - macOS application metadata
- `CatCommander/Images/cat.png` - Source PNG image for the icon

## Troubleshooting

### Icon not showing in Dock
1. Make sure you're running the `.app` bundle (not `dotnet run`)
2. Try resetting the Dock: `killall Dock`
3. Verify `cat.icns` exists in `CatCommander.app/Contents/Resources/`

### App won't launch
1. Check executable permissions: `chmod +x build/CatCommander.app/Contents/MacOS/launcher.sh`
2. View console logs: Open Console.app and filter for "CatCommander"

### Rebuilding
The script automatically cleans the `build/` directory before building, so you can run it multiple times safely.

## Architecture

The build script creates an **ARM64** (Apple Silicon) build by default. To build for Intel Macs, modify the script:

```bash
# Change this line in build-macos-app.sh:
-r osx-arm64
# To:
-r osx-x64
```

Or create a universal binary by building both architectures and using `lipo`.
