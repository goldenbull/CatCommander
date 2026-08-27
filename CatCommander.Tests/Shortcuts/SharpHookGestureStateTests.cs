using Avalonia.Input;
using CatCommander.Shortcuts;
using SharpHook.Data;

namespace CatCommander.Tests.Shortcuts;

public sealed class SharpHookGestureStateTests
{
    [Fact]
    public void TryPress_MapsCommandPeriod()
    {
        var state = new SharpHookGestureState();

        Assert.False(state.TryPress(KeyCode.VcLeftMeta, out _));
        Assert.True(state.TryPress(KeyCode.VcPeriod, out var gesture));

        Assert.Equal(Key.OemPeriod, gesture.Key);
        Assert.Equal(KeyModifiers.Meta, gesture.KeyModifiers);
    }

    [Fact]
    public void TryPress_MapsCommandNumberRow()
    {
        var state = new SharpHookGestureState();
        state.TryPress(KeyCode.VcLeftMeta, out _);

        Assert.True(state.TryPress(KeyCode.Vc1, out var gesture));
        Assert.Equal(new KeyGesture(Key.D1, KeyModifiers.Meta), gesture);
    }

    [Fact]
    public void Release_OnlyRemovesTheReleasedPhysicalModifier()
    {
        var state = new SharpHookGestureState();
        state.TryPress(KeyCode.VcLeftMeta, out _);
        state.TryPress(KeyCode.VcRightMeta, out _);

        state.Release(KeyCode.VcLeftMeta);
        Assert.True(state.TryPress(KeyCode.VcRight, out var gesture));

        Assert.Equal(KeyModifiers.Meta, gesture.KeyModifiers);
    }

    [Fact]
    public void Release_ClearsModifierFromLaterGestures()
    {
        var state = new SharpHookGestureState();
        state.TryPress(KeyCode.VcLeftControl, out _);
        state.Release(KeyCode.VcLeftControl);

        Assert.True(state.TryPress(KeyCode.VcF5, out var gesture));
        Assert.Equal(KeyModifiers.None, gesture.KeyModifiers);
    }

    [Fact]
    public void TryPress_RejectsUnmappedKey()
    {
        var state = new SharpHookGestureState();

        Assert.False(state.TryPress(KeyCode.VcUndefined, out _));
    }

    [Fact]
    public void InputContext_LowLevelHookYieldsPlainEscape_ToAvaloniaRouting()
    {
        var context = new ShortcutInputContext();

        Assert.True(context.ShouldYieldToActiveWindowConvention(new KeyGesture(Key.Escape)));
    }
}
