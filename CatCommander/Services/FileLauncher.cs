using System.Diagnostics;
using CatCommander.Config;
using CatCommander.Platform;
using NLog;

namespace CatCommander.Services;

public interface IFileLauncher
{
    void Preview(string path);
    void Edit(string path);
}

/// <summary>Platform boundary for F3 preview and F4 edit; never invokes a shell command string.</summary>
public sealed class FileLauncher : IFileLauncher
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();
    private readonly EditorSettings _settings;
    private readonly PlatformInfo _platform;

    public FileLauncher(ApplicationSettings settings, PlatformInfo platform)
    {
        _settings = settings.Editor;
        _platform = platform;
    }

    public void Preview(string path) => Process.Start(CreatePreviewStartInfo(path));
    public void Edit(string path)
    {
        var startInfo = CreateEditStartInfo(path);
        log.Info(
            "F4 launching editor: configured={0}, executable={1}, useShell={2}, verb={3}, arguments=[{4}]",
            string.IsNullOrWhiteSpace(_settings.Command) ? "<platform-default>" : _settings.Command,
            startInfo.FileName,
            startInfo.UseShellExecute,
            string.IsNullOrEmpty(startInfo.Verb) ? "<none>" : startInfo.Verb,
            string.Join(", ", startInfo.ArgumentList));
        Process.Start(startInfo);
    }

    public ProcessStartInfo CreatePreviewStartInfo(string path)
    {
        if (_platform.IsMacOS)
            return WithArgument("/usr/bin/qlmanage", path, "-p");

        if (_platform.IsWindows)
            return new ProcessStartInfo(path) { UseShellExecute = true };

        return WithArgument("xdg-open", path);
    }

    public ProcessStartInfo CreateEditStartInfo(string path)
    {
        if (!string.IsNullOrWhiteSpace(_settings.Command))
        {
            var command = _settings.Command.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (_platform.IsMacOS && command.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return WithArgument("/usr/bin/open", path, "-a", command);
            return WithArgument(command, path);
        }

        if (_platform.IsMacOS)
            return WithArgument("/usr/bin/open", path, "-e");

        if (_platform.IsWindows)
            return new ProcessStartInfo(path) { UseShellExecute = true, Verb = "edit" };

        return WithArgument("xdg-open", path);
    }

    private static ProcessStartInfo WithArgument(string executable, string path, params string[] leadingArguments)
    {
        var result = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in leadingArguments)
            result.ArgumentList.Add(argument);
        result.ArgumentList.Add(path);
        return result;
    }
}
