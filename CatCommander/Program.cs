using System;
using Avalonia;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.View;
using CatCommander.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using ReactiveUI.Avalonia;

namespace CatCommander;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        BuildAvaloniaApp(provider).StartWithClassicDesktopLifetime(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ConfigManager>();
        services.AddSingleton(sp => sp.GetRequiredService<ConfigManager>().Shortcuts);

        // One shared registry/cache for the whole app - registry only ever needs one
        // LocalFileSystemProviderFactory (registered last, unconditional catch-all - see its own
        // doc comment); IconCache's whole point is caching across every ItemBrowserViewModel.
        services.AddSingleton<FileSystemProviderRegistry>(_ =>
        {
            var registry = new FileSystemProviderRegistry();
            registry.Register(new LocalFileSystemProviderFactory());
            return registry;
        });
        services.AddSingleton<IconCache>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<FindViewModel>();
        services.AddTransient<BatchRenameViewModel>();
        services.AddTransient<MainPanelViewModel>();
        services.AddTransient<ItemBrowserViewModel>();

        services.AddTransient<MainWindow>();
        services.AddTransient<FindWindow>();
        services.AddTransient<BatchRenameWindow>();

        // Explicit Func<T> factories: Microsoft.Extensions.DependencyInjection doesn't
        // auto-synthesize these the way some other containers do. Needed here because
        // MainWindowViewModel/MainPanelViewModel each create more than one instance of the same
        // type (Left/RightPanel, per-tab ItemBrowserViewModel).
        services.AddTransient<Func<FindWindow>>(sp => () => sp.GetRequiredService<FindWindow>());
        services.AddTransient<Func<BatchRenameWindow>>(sp => () => sp.GetRequiredService<BatchRenameWindow>());
        services.AddTransient<Func<MainPanelViewModel>>(sp => () => sp.GetRequiredService<MainPanelViewModel>());
        services.AddTransient<Func<ItemBrowserViewModel>>(sp => () => sp.GetRequiredService<ItemBrowserViewModel>());
    }

    // Avalonia configuration. Takes the DI provider explicitly (via AppBuilder.Configure's
    // factory overload) since App no longer has a parameterless constructor - this means the
    // live XAML designer/previewer can't instantiate App on its own anymore.
    public static AppBuilder BuildAvaloniaApp(IServiceProvider services)
    {
        return AppBuilder.Configure(() => new App(services))
            .UsePlatformDetect()
            .WithInterFont()
            .UseReactiveUI(_ => { })
            .LogToTrace();
    }
}
