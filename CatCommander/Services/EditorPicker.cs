using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CatCommander.Platform;
using NLog;
using System.Diagnostics;

namespace CatCommander.Services;

public interface IEditorPicker
{
    Task<string?> PickAsync();
}

/// <summary>Keeps Avalonia's window-owned storage picker out of MainWindowViewModel.</summary>
public sealed class EditorPicker : IEditorPicker
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();
    private readonly PlatformInfo _platform;

    public EditorPicker(PlatformInfo platform) => _platform = platform;

    public async Task<string?> PickAsync()
    {
        if (_platform.IsMacOS)
            return await PickMacApplicationAsync();

        var window = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.Windows.FirstOrDefault(candidate => candidate.IsActive);
        if (window is null)
        {
            log.Warn("Editor picker was requested but no active window was found");
            return null;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose F4 Editor",
            AllowMultiple = false,
        });

        log.Info("Editor picker returned {0} item(s)", files.Count);
        if (files.Count != 1)
            return null;

        var selected = files[0];
        var localPath = selected.TryGetLocalPath();
        log.Info(
            "Editor picker selected item: name={0}, uri={1}, localPath={2}",
            selected.Name,
            selected.Path,
            localPath ?? "<null>");
        return localPath;
    }

    private static async Task<string?> PickMacApplicationAsync()
    {
        // Avalonia's picker treats .app bundles as selectable in the UI but returns an empty result.
        // Standard Additions' file picker has Finder-style navigation and returns the selected
        // application bundle as a native POSIX path without an intermediate application list.
        var startInfo = new ProcessStartInfo("/usr/bin/osascript")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("JavaScript");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(
            "const host = Application.currentApplication(); " +
            "host.includeStandardAdditions = true; " +
            "const editor = host.chooseFile({" +
            "withPrompt: \"Choose F4 Editor\", " +
            "ofType: [\"com.apple.application-bundle\"], " +
            "invisibles: false}); " +
            "editor.toString();");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            log.Warn("macOS editor application chooser failed to start");
            return null;
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            log.Info("macOS editor application chooser was cancelled or failed: exit={0}, error={1}",
                process.ExitCode, error.Trim());
            return null;
        }

        var path = output.Trim();
        log.Info("macOS editor application chooser selected localPath={0}",
            path.Length == 0 ? "<empty>" : path);
        return path.Length == 0 ? null : path;
    }
}
