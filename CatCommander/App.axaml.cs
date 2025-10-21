using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CatCommander.Configuration;
using NLog;

namespace CatCommander
{
    public partial class App : Application
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);

            // try interop
            log.Info(my_wrapper_func(100));
        }

        [LibraryImport("lib/7z")]
        private static partial int my_wrapper_func(int n);
        
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