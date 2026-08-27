using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.View;
using CatCommander.ViewModels;
using CatCommander.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace CatCommander;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private GlobalShortcutGuard? _globalShortcutGuard;

    public App(IServiceProvider services)
    {
        _services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var shortcuts = _services.GetRequiredService<ShortcutsSettings>();
            var inputContext = _services.GetRequiredService<ShortcutInputContext>();
            var inputState = _services.GetRequiredService<ShortcutInputState>();
            _globalShortcutGuard = new GlobalShortcutGuard(
                shortcuts,
                () => ActiveCommandSource(desktop),
                inputContext,
                _services.GetRequiredService<PlatformInfo>());
            inputState.LowLevelHookActive = _globalShortcutGuard.Start();

            // Construct windows after deciding the primary input source, so ShortcutRouter can
            // install only as the low-level hook's fallback.
            var mainWindow = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) =>
            {
                _services.GetRequiredService<ConfigManager>().SaveSession(
                    _services.GetRequiredService<MainWindowViewModel>().CaptureSession());
                _globalShortcutGuard?.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IShortcutCommandSource? ActiveCommandSource(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsActive);
        return (activeWindow as Window)?.DataContext as IShortcutCommandSource;
    }
}
