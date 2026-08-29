using System;
using Avalonia;
using CatCommander.Config;
using CatCommander.FileSystem;
using CatCommander.Services;
using CatCommander.Platform;
using CatCommander.Shortcuts;
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
        services.AddSingleton(PlatformInfo.Current);
        services.AddSingleton(sp => sp.GetRequiredService<ConfigManager>().Shortcuts);
        services.AddSingleton(sp => sp.GetRequiredService<ConfigManager>().Settings);

        // One shared registry/cache for the whole app - registry only ever needs one
        // LocalFileSystemProviderFactory (registered last, unconditional catch-all - see its own
        // doc comment); IconCache's whole point is caching across every ItemBrowserViewModel.
        services.AddSingleton<IArchivePasswordStore, ArchivePasswordStore>();
        services.AddSingleton<FileSystemProviderRegistry>(sp =>
        {
            var registry = new FileSystemProviderRegistry();
            registry.Register(new ArchiveFileSystemProviderFactory(sp.GetRequiredService<IArchivePasswordStore>()));
            registry.Register(new LocalFileSystemProviderFactory());
            return registry;
        });
        services.AddSingleton<IconCache>();

        // F5/F6's "system-level job list" - one shared queue/worker for the whole app (see its
        // own doc comment), independent of any single window.
        services.AddSingleton<FileOperationQueue>();
        services.AddSingleton<ResourceTransferService>();
        services.AddSingleton<BrowserCommandPolicy>();
        services.AddSingleton<ITerminalLauncher, TerminalLauncher>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<FileClipboardState>();
        services.AddSingleton<IArchivePasswordPrompt, ArchivePasswordPrompt>();
        services.AddSingleton<ShortcutInputContext>();
        services.AddSingleton<ShortcutInputState>();

        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<FindViewModel>();
        services.AddTransient<BatchRenameViewModel>();
        services.AddTransient<JobListViewModel>();
        services.AddTransient<MainPanelViewModel>();
        services.AddTransient<ItemBrowserViewModel>();

        services.AddTransient<MainWindow>();
        services.AddTransient<FindWindow>();
        services.AddTransient<BatchRenameWindow>();
        services.AddTransient<JobListWindow>();

        // Explicit Func<T> factories: Microsoft.Extensions.DependencyInjection doesn't
        // auto-synthesize these the way some other containers do. Needed here because
        // MainWindowViewModel/MainPanelViewModel each create more than one instance of the same
        // type (Left/RightPanel, per-tab ItemBrowserViewModel).
        //
        // FileOperationConfirmWindow/FileOperationProgressWindow/NewFolderWindow are NOT
        // registered here - their ViewModels need runtime parameters (a job, a create-callback)
        // that this factory pattern doesn't thread through, so MainWindowViewModel constructs
        // those directly instead (see StartFileOperation/OpenCreateDirectoryDialog).
        services.AddTransient<Func<FindWindow>>(sp => () => sp.GetRequiredService<FindWindow>());
        services.AddTransient<Func<BatchRenameWindow>>(sp => () => sp.GetRequiredService<BatchRenameWindow>());
        services.AddTransient<Func<JobListWindow>>(sp => () => sp.GetRequiredService<JobListWindow>());
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
