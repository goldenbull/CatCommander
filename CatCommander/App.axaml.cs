using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatCommander.Config;
using CatCommander.Shortcuts;
using CatCommander.View;
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
            var mainWindow = _services.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;

            var shortcuts = _services.GetRequiredService<ShortcutsSettings>();
            _globalShortcutGuard = new GlobalShortcutGuard(shortcuts, () => ActiveCommandSource(desktop));
            _globalShortcutGuard.Start();

            desktop.Exit += (_, _) => _globalShortcutGuard?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IShortcutCommandSource? ActiveCommandSource(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var activeWindow = desktop.Windows.FirstOrDefault(w => w.IsActive) ?? desktop.MainWindow;
        return (activeWindow as Window)?.DataContext as IShortcutCommandSource;
    }
}
