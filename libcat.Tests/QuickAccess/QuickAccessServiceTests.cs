using System.Linq;
using CatCommander.QuickAccess;
using Xunit;

namespace CatCommander.Tests.QuickAccess;

public class QuickAccessServiceTests
{
    [Fact]
    public void GetEntries_ReturnsAtLeastOneEntry()
    {
        var entries = QuickAccessService.GetEntries();
        Assert.NotEmpty(entries);
    }

    [Fact]
    public void GetEntries_AllPathsExist()
    {
        // Every entry should point at somewhere real on this machine - a stale/wrong path here
        // would silently break "click quick access button" in the UI.
        foreach (var entry in QuickAccessService.GetEntries())
            Assert.True(System.IO.Directory.Exists(entry.Path), $"{entry.DisplayName} -> {entry.Path}");
    }

    [Fact]
    public void GetEntries_IncludesHomeDirectoryOnNonWindows()
    {
        if (OperatingSystem.IsWindows())
            return;

        var entries = QuickAccessService.GetEntries();
        Assert.Contains(entries, e => e.Kind == QuickAccessKind.SpecialFolder && e.DisplayName == "Home");
    }

    [Fact]
    public void GetEntries_IncludesDriveLettersOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var entries = QuickAccessService.GetEntries();
        Assert.Contains(entries, e => e.Kind == QuickAccessKind.Drive);
    }
}
