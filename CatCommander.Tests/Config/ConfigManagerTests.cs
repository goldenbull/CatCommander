using Avalonia.Input;
using CatCommander.Config;

namespace CatCommander.Tests.Config;

public sealed class ConfigManagerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"CatCommanderConfig_{Guid.NewGuid():N}");

    [Fact]
    public void ConfigToml_RoundTripsShortcutsAndTerminalSettings()
    {
        var manager = new ConfigManager(_directory);
        manager.Settings.Shortcuts.Bindings["Copy"] = "Ctrl+C";
        manager.Settings.Terminal.WindowsShell = "powershell";
        manager.SaveSettings();

        var reloaded = new ConfigManager(_directory);

        Assert.Equal("powershell", reloaded.Settings.Terminal.WindowsShell);
        Assert.Equal(Operation.Copy, reloaded.Shortcuts.GetOperation(KeyGesture.Parse("Ctrl+C")));
        Assert.Contains("[shortcuts.bindings]", File.ReadAllText(Path.Combine(_directory, "config.toml")));
    }

    [Fact]
    public void LegacyKeymap_IsMigratedIntoUnifiedConfig()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "keymap.toml"), "[bindings]\nCopy = \"Ctrl+C\"\n");

        var manager = new ConfigManager(_directory);

        Assert.Equal(Operation.Copy, manager.Shortcuts.GetOperation(KeyGesture.Parse("Ctrl+C")));
        Assert.True(File.Exists(Path.Combine(_directory, "config.toml")));
    }

    [Fact]
    public void Session_RoundTripsBothPanelsAndActiveIndices()
    {
        var manager = new ConfigManager(_directory);
        var state = new SessionState
        {
            ActivePanel = "right",
            Left = new PanelSessionState { Tabs = ["/a", "/b"], ActiveTab = 1 },
            Right = new PanelSessionState { Tabs = ["/c"], ActiveTab = 0 },
        };

        manager.SaveSession(state);
        var restored = manager.LoadSession();

        Assert.NotNull(restored);
        Assert.Equal("right", restored.ActivePanel);
        Assert.Equal(["/a", "/b"], restored.Left.Tabs);
        Assert.Equal(1, restored.Left.ActiveTab);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
