using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatCommander.Services;

namespace CatCommander
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // Initialize logging
            LoggingService.Initialize();
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();

                // Handle application exit
                desktop.Exit += (sender, args) =>
                {
                    LoggingService.Shutdown();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}