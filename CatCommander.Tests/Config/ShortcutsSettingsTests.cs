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
        Assert.Equal("Meta+G", defaults[Operation.OpenTerminal]);
        Assert.Equal("Meta+1", defaults[Operation.CopyContainerPath]);
        Assert.Equal("Meta+2", defaults[Operation.CopyItemNames]);
        Assert.Equal("Meta+3", defaults[Operation.CopyItemPaths]);
        Assert.Equal("Meta+F3", defaults[Operation.SortByName]);
        Assert.Equal("Meta+F4", defaults[Operation.SortByExtension]);
        Assert.Equal("Meta+F5", defaults[Operation.SortBySize]);
        Assert.Equal("Meta+F6", defaults[Operation.SortByDate]);

        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(KeyboardStyle.MacOS);
        Assert.Equal(Operation.CopyContainerPath,
            settings.GetOperation(new KeyGesture(Key.D1, KeyModifiers.Meta)));
    }

    [Fact]
    public void GetDefaults_WindowsStyle_UsesCtrlForPrimaryModifier()
    {
        var defaults = ShortcutsSettings.GetDefaults(KeyboardStyle.Windows);

        Assert.Equal("Ctrl+B", defaults[Operation.ExpandCurrentFolder]);
        Assert.Equal("Ctrl+M", defaults[Operation.OpenBatchRename]);
        Assert.Equal("Ctrl+G", defaults[Operation.OpenTerminal]);
        Assert.Equal("Ctrl+1", defaults[Operation.CopyContainerPath]);
        Assert.Equal("Ctrl+2", defaults[Operation.CopyItemNames]);
        Assert.Equal("Ctrl+3", defaults[Operation.CopyItemPaths]);
        Assert.Equal("Ctrl+F3", defaults[Operation.SortByName]);
        Assert.Equal("Ctrl+F4", defaults[Operation.SortByExtension]);
        Assert.Equal("Ctrl+F5", defaults[Operation.SortBySize]);
        Assert.Equal("Ctrl+F6", defaults[Operation.SortByDate]);
    }

    [Fact]
    public void GetDefaults_MacOSStyle_UsesMetaUpForOpenSelectedFolderInNewTab()
    {
        var defaults = ShortcutsSettings.GetDefaults(KeyboardStyle.MacOS);

        Assert.Equal("Meta+Up", defaults[Operation.OpenSelectedFolderInNewTab]);
    }

    [Fact]
    public void GetDefaults_CrossPanelBindings_PreserveDirection()
    {
        var windows = ShortcutsSettings.GetDefaults(KeyboardStyle.Windows);
        var mac = ShortcutsSettings.GetDefaults(KeyboardStyle.MacOS);

        Assert.Equal("Ctrl+Left", windows[Operation.OpenCurrentFolderInLeftPanel]);
        Assert.Equal("Ctrl+Right", windows[Operation.OpenCurrentFolderInRightPanel]);
        Assert.Equal("Meta+Left", mac[Operation.OpenCurrentFolderInLeftPanel]);
        Assert.Equal("Meta+Right", mac[Operation.OpenCurrentFolderInRightPanel]);
    }

    [Fact]
    public void GetDefaults_WindowsStyle_UsesCtrlUpForOpenSelectedFolderInNewTab()
    {
        var defaults = ShortcutsSettings.GetDefaults(KeyboardStyle.Windows);

        Assert.Equal("Ctrl+Up", defaults[Operation.OpenSelectedFolderInNewTab]);
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

    // GetPrimaryGesture backs MainWindow.axaml.cs's NativeMenuItem.Gesture assignment - the whole
    // point is that it can never drift from what ShortcutRouter would actually dispatch for the
    // same Operation, so these assert it stays derived from the very same effective bindings
    // GetOperation resolves against, not a second, independent source.

    [Fact]
    public void GetPrimaryGesture_ReturnsTheFirstListedAlternative_ByDefault()
    {
        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(KeyboardStyle.Windows);

        // Rename's default is "Shift+F6;F2" - Shift+F6 is first.
        Assert.Equal(KeyGesture.Parse("Shift+F6"), settings.GetPrimaryGesture(Operation.Rename));
    }

    [Fact]
    public void GetPrimaryGesture_ReflectsAUserOverride()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "Ctrl+C";
        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Equal(KeyGesture.Parse("Ctrl+C"), settings.GetPrimaryGesture(Operation.Copy));
    }

    [Fact]
    public void GetPrimaryGesture_ReturnsNull_ForAnOperationWithNoBinding()
    {
        var settings = new ShortcutsSettings();
        settings.RebuildNormalized(KeyboardStyle.Windows);

        Assert.Null(settings.GetPrimaryGesture(Operation.Nop));
    }

    [Fact]
    public void GetPrimaryGesture_StaysInSyncWithGetOperation_AfterRestoreDefaults()
    {
        var settings = new ShortcutsSettings();
        settings.Bindings["Copy"] = "Ctrl+C";
        settings.RebuildNormalized(KeyboardStyle.Windows);

        settings.RestoreDefaults();
        settings.RebuildNormalized(KeyboardStyle.Windows);

        var primary = settings.GetPrimaryGesture(Operation.Copy);
        Assert.NotNull(primary);
        Assert.Equal(Operation.Copy, settings.GetOperation(primary!));
    }
}
