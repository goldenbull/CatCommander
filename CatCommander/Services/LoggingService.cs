using System;
using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace CatCommander.Services;

public static class LoggingService
{
    private static ILoggerFactory? _loggerFactory;

    public static void Initialize()
    {
        // Set up Serilog configuration
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CatCommander",
            "Logs");

        Directory.CreateDirectory(logDirectory);

        var logFilePath = Path.Combine(logDirectory, "catcommander-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _loggerFactory = new SerilogLoggerFactory(Log.Logger);

        Log.Information("CatCommander logging initialized");
        Log.Information("Log file location: {LogDirectory}", logDirectory);
    }

    public static ILogger<T> CreateLogger<T>()
    {
        if (_loggerFactory == null)
        {
            Initialize();
        }

        return _loggerFactory!.CreateLogger<T>();
    }

    public static void Shutdown()
    {
        Log.Information("CatCommander shutting down");
        Log.CloseAndFlush();
    }
}
