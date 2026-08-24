using System;
using Avalonia;
using CatCommander.Config;
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

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<FindViewModel>();
        services.AddTransient<BatchRenameViewModel>();

        services.AddTransient<MainWindow>();
        services.AddTransient<FindWindow>();
        services.AddTransient<BatchRenameWindow>();

        // Explicit Func<T> factories: Microsoft.Extensions.DependencyInjection doesn't
        // auto-synthesize these the way some other containers do.
        services.AddTransient<Func<FindWindow>>(sp => () => sp.GetRequiredService<FindWindow>());
        services.AddTransient<Func<BatchRenameWindow>>(sp => () => sp.GetRequiredService<BatchRenameWindow>());
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
