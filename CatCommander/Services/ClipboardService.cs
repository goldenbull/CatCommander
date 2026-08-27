using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace CatCommander.Services;

/// <summary>Keeps Avalonia's window-owned clipboard API out of browser ViewModels.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
    Task SetFilesAsync(IReadOnlyList<string> paths);
}

public sealed class ClipboardService : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard =
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }

    public async Task SetFilesAsync(IReadOnlyList<string> paths)
    {
        var window =
            (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (window?.Clipboard is not { } clipboard)
            return;

        var files = new List<IStorageItem>();
        foreach (var path in paths)
        {
            IStorageItem? item = Directory.Exists(path)
                ? await window.StorageProvider.TryGetFolderFromPathAsync(path)
                : await window.StorageProvider.TryGetFileFromPathAsync(path);
            if (item is not null)
                files.Add(item);
        }

        if (files.Count > 0)
            await clipboard.SetFilesAsync(files);
    }
}
