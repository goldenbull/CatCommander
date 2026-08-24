#!/bin/bash

# Build script for creating CatCommander.app bundle on macOS
# This creates a proper macOS application with the cat icon

set -e  # Exit on error

echo "🐱 Building CatCommander.app for macOS..."
echo ""

# Configuration
PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ_PATH="$PROJECT_DIR/CatCommander/CatCommander.csproj"
OUTPUT_DIR="$PROJECT_DIR/build"
APP_NAME="CatCommander"
APP_BUNDLE="$OUTPUT_DIR/$APP_NAME.app"

# Clean previous build
echo "🧹 Cleaning previous build..."
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Build the project
echo "🔨 Building .NET project..."
dotnet publish "$CSPROJ_PATH" \
    -c Release \
    -r osx-arm64 \
    --self-contained true \
    -p:PublishSingleFile=false \
    -p:PublishTrimmed=false \
    -o "$OUTPUT_DIR/publish"

echo ""
echo "📦 Creating .app bundle structure..."

# Create app bundle structure
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy executable and dependencies
echo "📋 Copying application files..."
cp -R "$OUTPUT_DIR/publish/"* "$APP_BUNDLE/Contents/MacOS/"

# Copy icon file
echo "🎨 Adding application icon..."
cp "$PROJECT_DIR/CatCommander/Assets/cat.icns" "$APP_BUNDLE/Contents/Resources/"

# Copy Info.plist
echo "📄 Adding Info.plist..."
cp "$PROJECT_DIR/CatCommander/Assets/Info.plist" "$APP_BUNDLE/Contents/"

# Make executable
chmod +x "$APP_BUNDLE/Contents/MacOS/$APP_NAME"

echo ""
echo "✅ Build complete!"
echo ""
echo "📍 Application location: $APP_BUNDLE"
echo ""
echo "🚀 To run the application:"
echo "   Option 1: Double-click: $APP_BUNDLE"
echo "   Option 2: Run: open \"$APP_BUNDLE\""
echo ""
echo "📥 To install to Applications folder:"
echo "   cp -R \"$APP_BUNDLE\" /Applications/"
echo ""
