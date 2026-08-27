using System;
using Avalonia.Input;
using Avalonia.Threading;
using CatCommander.Config;
using CatCommander.Utils;
using CatCommander.Platform;
using NLog;
using SharpHook;
using SharpHook.Data;

namespace CatCommander.Shortcuts;

/// <summary>
/// Primary input path for every configured shortcut. This also covers gestures macOS or another
/// application consumes before Avalonia's KeyDown pipeline sees them. SimpleGlobalHook is the only
/// SharpHook hook type that
/// supports synchronously suppressing an event (KeyboardHookEventArgs.SuppressEvent, set inside
/// the handler) - EventLoopGlobalHook/TaskPoolGlobalHook run handlers on another thread, too late
/// for suppression to take effect.
///
/// Unbound gestures and gestures owned by a focused text editor are never suppressed. A bound
/// gesture is suppressed synchronously, then availability and execution are resolved on the UI
/// thread. ShortcutRouter remains a passive startup/permission-failure fallback for gestures that
/// still reach Avalonia.
/// </summary>
public sealed class GlobalShortcutGuard : IDisposable
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    private readonly SimpleGlobalHook _hook;
    private readonly ShortcutsSettings _settings;
    private readonly Func<IShortcutCommandSource?> _activeCommandSourceProvider;
    private readonly ShortcutInputContext _inputContext;
    private readonly PlatformInfo _platform;
    private readonly SharpHookGestureState _gestureState = new();

    /// <param name="settings">Same ShortcutsSettings instance ShortcutRouter uses - one source of truth.</param>
    /// <param name="activeCommandSourceProvider">
    /// Resolves "whichever ViewModel should currently answer an Operation" - typically the active
    /// window's DataContext. Supplied by the composition root, which is the only place that knows
    /// about the live Window set.
    /// </param>
    public GlobalShortcutGuard(
        ShortcutsSettings settings,
        Func<IShortcutCommandSource?> activeCommandSourceProvider,
        ShortcutInputContext inputContext,
        PlatformInfo? platform = null)
    {
        _settings = settings;
        _activeCommandSourceProvider = activeCommandSourceProvider;
        _inputContext = inputContext;
        _platform = platform ?? PlatformInfo.Current;
        _hook = new SimpleGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
    }

    /// <summary>
    /// Starts the global hook when foreground-safe capture is available.
    /// </summary>
    public bool Start()
    {
        // The foreground-process guard is currently implemented only for macOS. Starting a global
        // suppressing hook on another platform without an equivalent guard could steal configured
        // gestures while another application is active; use Avalonia's fallback there for now.
        if (!_platform.IsMacOS)
        {
            log.Info("GlobalShortcutGuard not started: foreground-safe low-level capture is currently macOS-only");
            return false;
        }

        try
        {
            _hook.RunAsync();
            log.Info("GlobalShortcutGuard started as the primary shortcut input source");
            return true;
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to start GlobalShortcutGuard - falling back to Avalonia shortcut input");
            return false;
        }
    }

    public void Dispose() => _hook.Dispose();

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        // Never let a managed exception cross SharpHook's native event-tap callback boundary.
        // macOS/.NET turns such an exception into process-wide SIGABRT rather than routing it to
        // Avalonia's normal unhandled-exception machinery.
        try
        {
            HandleKeyPressed(e);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Unhandled error while processing SharpHook key press {0}", e.Data.KeyCode);
        }
    }

    private void HandleKeyPressed(KeyboardHookEventArgs e)
    {
        if (!_gestureState.TryPress(e.Data.KeyCode, out var gesture))
            return;
        var operation = _settings.GetOperation(gesture);
        if (operation == Operation.Nop)
            return;

        // Must check focus before deciding to suppress: if CatCommander isn't the frontmost app,
        // this gesture has nothing to do with us - let macOS/whichever app is focused handle it
        // exactly as it normally would.
        if (!ForegroundAppChecker.IsFrontmostApplication())
            return;

        // Enter/Escape are dialog conventions, not operations, while a dialog is active. Yield
        // before suppression so the Window's tunnel handler can run even when the same gesture is
        // present in the configurable keymap (Enter normally means GoIntoCurrentFolder).
        if (_inputContext.ShouldYieldToActiveWindowConvention(gesture))
            return;

        if (TextEditKeyExceptions.ShouldYieldToTextEditing(gesture, _inputContext.IsTextEditing))
            return;

        // Must be set synchronously inside this handler - see the class doc on why SimpleGlobalHook.
        e.SuppressEvent = true;

        // Window lookup, command routing, and CanExecute all touch Avalonia/ViewModel state and must
        // happen on the UI thread. Doing any of them synchronously in this native callback caused
        // the Cmd+. crash recorded in macOS DiagnosticReports (SIGABRT on dispatch_key_press).
        Dispatcher.UIThread.Post(() => DispatchOnUiThread(operation));

        log.Debug("GlobalShortcutGuard suppressed + queued {0} for {1}", operation, gesture);
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        try
        {
            _gestureState.Release(e.Data.KeyCode);
        }
        catch (Exception ex)
        {
            log.Error(ex, "Unhandled error while processing SharpHook key release {0}", e.Data.KeyCode);
        }
    }

    private void DispatchOnUiThread(Operation operation)
    {
        try
        {
            var command = _activeCommandSourceProvider()?.GetCommand(operation);
            if (command?.CanExecute(null) == true)
            {
                command.Execute(null);
                log.Debug("GlobalShortcutGuard dispatched {0} on UI thread", operation);
            }
            else
            {
                log.Debug("GlobalShortcutGuard ignored unavailable operation {0} on UI thread", operation);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to dispatch SharpHook operation {0} on UI thread", operation);
        }
    }

}
