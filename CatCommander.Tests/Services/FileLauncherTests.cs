using CatCommander.Config;
using CatCommander.Platform;
using CatCommander.Services;

namespace CatCommander.Tests.Services;

public sealed class FileLauncherTests
{
    [Fact]
    public void MacOSPreview_UsesQuickLookWithoutShellInterpolation()
    {
        var launcher = new FileLauncher(new ApplicationSettings(), new PlatformInfo(PlatformKind.MacOS));

        var info = launcher.CreatePreviewStartInfo("/tmp/a file.txt");

        Assert.Equal("/usr/bin/qlmanage", info.FileName);
        Assert.Equal(["-p", "/tmp/a file.txt"], info.ArgumentList);
        Assert.False(info.UseShellExecute);
    }

    [Fact]
    public void MacOSDefaultEditor_UsesTextEdit()
    {
        var launcher = new FileLauncher(new ApplicationSettings(), new PlatformInfo(PlatformKind.MacOS));

        var info = launcher.CreateEditStartInfo("/tmp/a file.txt");

        Assert.Equal("/usr/bin/open", info.FileName);
        Assert.Equal(["-e", "/tmp/a file.txt"], info.ArgumentList);
    }

    [Fact]
    public void ConfiguredEditor_ReceivesPathAsSeparateArgument()
    {
        var settings = new ApplicationSettings();
        settings.Editor.Command = "/Applications/Visual Studio Code.app/Contents/Resources/app/bin/code";
        var launcher = new FileLauncher(settings, new PlatformInfo(PlatformKind.MacOS));

        var info = launcher.CreateEditStartInfo("/tmp/a file.txt");

        Assert.Equal(settings.Editor.Command, info.FileName);
        Assert.Equal(["/tmp/a file.txt"], info.ArgumentList);
        Assert.False(info.UseShellExecute);
    }

    [Fact]
    public void ConfiguredMacAppBundle_IsOpenedAsAnApplication()
    {
        var settings = new ApplicationSettings();
        settings.Editor.Command = "/Applications/Visual Studio Code.app/";
        var launcher = new FileLauncher(settings, new PlatformInfo(PlatformKind.MacOS));

        var info = launcher.CreateEditStartInfo("/tmp/a file.txt");

        Assert.Equal("/usr/bin/open", info.FileName);
        Assert.Equal(["-a", "/Applications/Visual Studio Code.app", "/tmp/a file.txt"], info.ArgumentList);
    }
}
