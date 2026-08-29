using Avalonia.Input;
using CatCommander.Shortcuts;

namespace CatCommander.Tests.Shortcuts;

public class TextEditKeyExceptionsTests
{
    [Theory]
    [InlineData(Key.Enter, KeyModifiers.None)]
    [InlineData(Key.Escape, KeyModifiers.None)]
    [InlineData(Key.Tab, KeyModifiers.None)]
    [InlineData(Key.Left, KeyModifiers.None)]
    [InlineData(Key.C, KeyModifiers.Control)]
    [InlineData(Key.C, KeyModifiers.Meta)]
    [InlineData(Key.V, KeyModifiers.Control)]
    [InlineData(Key.Left, KeyModifiers.Alt)]
    [InlineData(Key.Right, KeyModifiers.Alt)]
    [InlineData(Key.Left, KeyModifiers.Meta | KeyModifiers.Shift)]
    public void IsReserved_True_ForKnownTextEditingGestures(Key key, KeyModifiers modifiers)
    {
        Assert.True(TextEditKeyExceptions.IsReserved(new KeyGesture(key, modifiers)));
    }

    [Fact]
    public void IsReserved_False_ForF5()
    {
        Assert.False(TextEditKeyExceptions.IsReserved(new KeyGesture(Key.F5)));
    }

    [Fact]
    public void ShouldYieldToTextEditing_False_WhenFocusNotEditable()
    {
        var gesture = new KeyGesture(Key.C, KeyModifiers.Control);

        Assert.False(TextEditKeyExceptions.ShouldYieldToTextEditing(gesture, focusIsEditable: false));
    }

    [Fact]
    public void ShouldYieldToTextEditing_True_WhenFocusEditableAndReserved()
    {
        // The core scenario this whole mechanism exists for: Ctrl+C is bound to "copy files" in
        // ShortcutsSettings, but a focused TextBox should keep it for text copy.
        var gesture = new KeyGesture(Key.C, KeyModifiers.Control);

        Assert.True(TextEditKeyExceptions.ShouldYieldToTextEditing(gesture, focusIsEditable: true));
    }

    [Fact]
    public void ShouldYieldToTextEditing_False_WhenFocusEditableButNotReserved()
    {
        // F-keys stay global operations regardless of focus, matching Total Commander behavior.
        var gesture = new KeyGesture(Key.F5);

        Assert.False(TextEditKeyExceptions.ShouldYieldToTextEditing(gesture, focusIsEditable: true));
    }
}
