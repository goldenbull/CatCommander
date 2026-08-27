using System.Diagnostics;
using CatCommander.Config;
using CatCommander.Platform;

namespace CatCommander.Services;

public interface ITerminalLauncher
{
    void Open(string directory);
}

public sealed class TerminalLauncher : ITerminalLauncher
{
    private readonly TerminalSettings _settings;
    private readonly PlatformInfo _platform;

    public TerminalLauncher(ApplicationSettings settings, PlatformInfo platform)
    {
        _settings = settings.Terminal;
        _platform = platform;
    }

    public void Open(string directory) => Process.Start(CreateStartInfo(directory));

    public ProcessStartInfo CreateStartInfo(string directory)
    {
        if (_platform.IsWindows)
        {
            var powershell = string.Equals(_settings.WindowsShell, "powershell", StringComparison.OrdinalIgnoreCase);
            return new ProcessStartInfo
            {
                FileName = powershell ? "powershell.exe" : "cmd.exe",
                WorkingDirectory = directory,
                UseShellExecute = true,
            };
        }

        if (_platform.IsMacOS)
        {
            var result = new ProcessStartInfo("/usr/bin/open") { UseShellExecute = false };
            result.ArgumentList.Add("-a");
            result.ArgumentList.Add("Terminal");
            result.ArgumentList.Add(directory);
            return result;
        }

        return new ProcessStartInfo("x-terminal-emulator")
        {
            WorkingDirectory = directory,
            UseShellExecute = false,
        };
    }

}
