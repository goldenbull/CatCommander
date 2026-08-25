using Avalonia.Input;
using CatCommander.Shortcuts;

namespace CatCommander.Tests.Shortcuts;

public class MacReservedCombosTests
{
    [Theory]
    [InlineData(Key.Left, KeyModifiers.Control)]
    [InlineData(Key.Right, KeyModifiers.Control)]
    [InlineData(Key.Up, KeyModifiers.Control)]
    [InlineData(Key.Down, KeyModifiers.Control)]
    public void Contains_True_ForMissionControlDesktopSwitching(Key key, KeyModifiers modifiers)
    {
        Assert.True(MacReservedCombos.Contains(new KeyGesture(key, modifiers)));
    }

    [Fact]
    public void Contains_True_ForBareF3()
    {
        Assert.True(MacReservedCombos.Contains(new KeyGesture(Key.F3)));
    }

    [Fact]
    public void Contains_True_ForCtrlTab_NativeWindowTabCycling()
    {
        Assert.True(MacReservedCombos.Contains(new KeyGesture(Key.Tab, KeyModifiers.Control)));
    }

    [Fact]
    public void Contains_False_ForUnrelatedGesture()
    {
        Assert.False(MacReservedCombos.Contains(new KeyGesture(Key.C, KeyModifiers.Control)));
    }
}
