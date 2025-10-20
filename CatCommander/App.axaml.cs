using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatCommander.Configuration;

namespace CatCommander
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Load configuration
            var configManager = ConfigManager.Instance;
            configManager.Load();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();

                // Handle application exit
                desktop.Exit += (sender, args) =>
                {
                    // Save configuration on exit if needed
                    configManager.Save();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}