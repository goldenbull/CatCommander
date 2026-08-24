using Avalonia.Input;
using CatCommander.Config;

namespace CatCommander.Tests.Config;

public class ShortcutsSettingsTests
{
    [Theory]
    [InlineData(KeyboardStyle.Windows)]
    [InlineData(KeyboardStyle.MacOS)]
    public void RebuildNormalized_UsesDefaults_WhenBindingsEmpty(KeyboardStyle style)
    {
        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(style);

        Assert.Equal(Operation.Copy, settings.GetOperation(KeyGesture.Parse("F5")));
        Assert.Equal(Operation.Move, settings.GetOperation(KeyGesture.Parse("F6")));
    }

    [Fact]
    public void RebuildNormalized_UserBindingWins_OverDefault()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "Ctrl+C";
        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Equal(Operation.Copy, settings.GetOperation(KeyGesture.Parse("Ctrl+C")));
        // The default "F5" binding for Copy is replaced, not kept as an extra alternative.
        Assert.Equal(Operation.Nop, settings.GetOperation(KeyGesture.Parse("F5")));
    }

    [Fact]
    public void RebuildNormalized_DoesNotMutateBindings()
    {
        // Bindings must stay user-deltas-only, since it's what gets serialized to keymap.toml -
        // the resolved (defaults + overrides) result must live only in the runtime lookup map.
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "Ctrl+C";

        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Single(settings.Bindings);
        Assert.Equal("Ctrl+C", settings.Bindings["Copy"]);
    }

    [Fact]
    public void RebuildNormalized_SupportsSemicolonSeparatedAlternatives()
    {
        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Equal(Operation.Rename, settings.GetOperation(KeyGesture.Parse("Shift+F6")));
        Assert.Equal(Operation.Rename, settings.GetOperation(KeyGesture.Parse("F2")));
    }

    [Fact]
    public void RebuildNormalized_UnboundGesture_ReturnsNop()
    {
        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Equal(Operation.Nop, settings.GetOperation(KeyGesture.Parse("Ctrl+Alt+Shift+Q")));
    }

    [Fact]
    public void RebuildNormalized_UnknownOperationName_IsSkippedNotThrown()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["SomeRemovedOrTypoedOperation"] = "Ctrl+Z";

        var ex = Record.Exception(() => settings.RebuildNormalized(KeyboardStyle.Windows));

        Assert.Null(ex);
    }

    [Fact]
    public void RebuildNormalized_UnparsableKeyString_IsSkippedNotThrown()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "NotAValidKey";

        var ex = Record.Exception(() => settings.RebuildNormalized(KeyboardStyle.Windows));

        Assert.Null(ex);
    }

    [Fact]
    public void RebuildNormalized_ConflictingBindings_ResolveToOneOperation_NotThrow()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "F1";
        settings.Bindings["Move"] = "F1";

        settings.RebuildNormalized(KeyboardStyle.Windows);
        var op = settings.GetOperation(KeyGesture.Parse("F1"));

        Assert.True(op is Operation.Copy or Operation.Move);
    }

    [Fact]
    public void GetDefaults_MacOSStyle_UsesMetaForPrimaryModifier()
    {
        var defaults = ShortcutsSettings.GetDefaults(KeyboardStyle.MacOS);

        Assert.Equal("Meta+B", defaults[Operation.ExpandCurrentFolder]);
        Assert.Equal("Meta+M", defaults[Operation.OpenBatchRename]);
    }

    [Fact]
    public void GetDefaults_WindowsStyle_UsesCtrlForPrimaryModifier()
    {
        var defaults = ShortcutsSettings.GetDefaults(KeyboardStyle.Windows);

        Assert.Equal("Ctrl+B", defaults[Operation.ExpandCurrentFolder]);
        Assert.Equal("Ctrl+M", defaults[Operation.OpenBatchRename]);
    }

    [Fact]
    public void GetDefaults_MacOSStyle_KeepsCtrlTabForSwitchTab_ToAvoidAppSwitcherCollision()
    {
        // Cmd+Tab is macOS's own system-wide app switcher - a true OS-level reservation, unlike
        // the other Ctrl-in-Windows/Cmd-in-macOS pairs which only collide with app conventions.
        var defaults = ShortcutsSettings.GetDefaults(KeyboardStyle.MacOS);

        Assert.Equal("Ctrl+Tab", defaults[Operation.SwitchTabInSamePanel]);
    }

    [Fact]
    public void RestoreDefaults_ClearsUserBindings()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "Ctrl+C";
        settings.RebuildNormalized(KeyboardStyle.Windows);

        settings.RestoreDefaults();

        Assert.Empty(settings.Bindings);
    }

    [Fact]
    public void RestoreDefaults_ThenRebuild_FallsBackToStyleDefault()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "Ctrl+C";
        settings.RebuildNormalized(KeyboardStyle.Windows);

        settings.RestoreDefaults();
        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Equal(Operation.Copy, settings.GetOperation(KeyGesture.Parse("F5")));
        Assert.Equal(Operation.Nop, settings.GetOperation(KeyGesture.Parse("Ctrl+C")));
    }
}
