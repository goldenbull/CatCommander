using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace CatCommander.Shortcuts;

/// <summary>
/// Thread-safe focus snapshot for the SharpHook callback. The hook thread must not inspect the
/// Avalonia visual tree synchronously while deciding whether to suppress an event.
/// </summary>
public sealed class ShortcutInputContext
{
    private int _isTextEditing;

    public bool IsTextEditing => Volatile.Read(ref _isTextEditing) != 0;

    public void Track(TopLevel topLevel)
    {
        topLevel.AddHandler(
            InputElement.GotFocusEvent,
            (_, e) => Volatile.Write(
                ref _isTextEditing,
                TextEditKeyExceptions.IsEditableControl(e.Source) ? 1 : 0),
            RoutingStrategies.Tunnel);
    }
}

public sealed class ShortcutInputState
{
    public bool LowLevelHookActive { get; set; }
}
