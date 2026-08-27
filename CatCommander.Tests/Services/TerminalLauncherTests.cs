using CatCommander.Config;
using CatCommander.Services;
using CatCommander.Platform;

namespace CatCommander.Tests.Services;

public sealed class TerminalLauncherTests
{
    [Theory]
    [InlineData("cmd", "cmd.exe")]
    [InlineData("powershell", "powershell.exe")]
    [InlineData("PoWeRsHeLl", "powershell.exe")]
    public void WindowsShell_IsSelectedFromConfiguration(string setting, string executable)
    {
        var settings = new ApplicationSettings();
        settings.Terminal.WindowsShell = setting;

        var info = new TerminalLauncher(settings, new PlatformInfo(PlatformKind.Windows)).CreateStartInfo("C:\\work");

        Assert.Equal(executable, info.FileName);
        Assert.Equal("C:\\work", info.WorkingDirectory);
    }

    [Fact]
    public void MacOS_OpensTerminalAtDirectoryWithoutShellEscaping()
    {
        var info = new TerminalLauncher(new ApplicationSettings(), new PlatformInfo(PlatformKind.MacOS))
            .CreateStartInfo("/tmp/a folder");

        Assert.Equal("/usr/bin/open", info.FileName);
        Assert.Equal(["-a", "Terminal", "/tmp/a folder"], info.ArgumentList);
    }
}
