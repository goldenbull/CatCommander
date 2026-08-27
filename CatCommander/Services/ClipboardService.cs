using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;

namespace CatCommander.Services;

/// <summary>Keeps Avalonia's window-owned clipboard API out of browser ViewModels.</summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
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
}
