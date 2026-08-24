using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatCommander.Config;
using NLog;

namespace CatCommander
{
    public partial class App : Application
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new View.MainWindow();

                // Handle application exit
                desktop.Exit += (sender, args) =>
                {
                    // Save configuration on exit if needed
                    ConfigManager.Instance.Save();
                };
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}