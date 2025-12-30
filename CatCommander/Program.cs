using System;
using System.IO;
using Avalonia;
using NLog;
using NLog.Config;
using Tomlyn;

namespace CatCommander
{
    internal class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize nlog before anything else
            InitializeNLog();

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        private static void InitializeNLog()
        {
            try
            {
                // Get NLog config path from app.toml
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var nlogConfigPath = Path.Combine(appDir, "Config", "NLog.config");
                // Load NLog configuration
                if (File.Exists(nlogConfigPath))
                {
                    LogManager.Configuration = new XmlLoggingConfiguration(nlogConfigPath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing NLog: {ex.Message}");
                // Continue with auto-discovery
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                             .UsePlatformDetect()
                             .WithInterFont()
                             .LogToTrace();
        }
    }
}