using System;
using System.Collections.Generic;
using Avalonia.Input;
using NLog;

namespace CatCommander.Config;

/// <summary>
/// Which primary modifier key convention the default shortcuts use - Ctrl on Windows/Linux, Cmd
/// (Meta) on macOS, since real macOS users expect Cmd for the shortcuts that would use Ctrl on
/// Windows. See GetDefaults for the one deliberate exception (SwitchTabInSamePanel). Always
/// derived from the running OS via ShortcutsSettings.CurrentStyle - not user-configurable, so
/// there is exactly one default keymap per machine and nothing to store or drift.
/// </summary>
public enum KeyboardStyle
{
    Windows,
    MacOS,
}

// Identifiers for both keys in the TOML config file and commands dispatched by IShortcutCommandSource.
public enum Operation
{
    Nop, // non-op, so lookups can return a value instead of null
    Copy,
    Move,
    Rename,
    Delete,
    CreateDirectory,
    ExpandCurrentFolder,
    ExpandSelectedFolders,
    GoIntoCurrentFolder,
    GoBackToParentFolder,
    GotoFirstItem,
    GotoLastItem,
    ReverseSelection,
    Refresh,
    ToggleHiddenFiles,
    OpenSelectedFolderInNewTab,
    SwitchTabInSamePanel,
    SwitchPanel,
    CloseTab,
    OpenCurrentFolderInOppositePanel,
    OpenFind,
    OpenBatchRename,
}

/// <summary>
/// Keyboard shortcut bindings, loadable from/savable to TOML via Tomlyn.
/// </summary>
public class ShortcutsSettings
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The only KeyboardStyle CatCommander ever resolves defaults with at runtime - derived
    /// straight from the OS it's running on, never stored or user-selectable. Keeps "which default
    /// keymap applies" a hardcoded fact instead of a setting that can drift from the real platform.
    /// </summary>
    public static KeyboardStyle CurrentStyle => OperatingSystem.IsMacOS() ? KeyboardStyle.MacOS : KeyboardStyle.Windows;

    /// <summary>
    /// Forward map: Operation name -> key bindings in string format, as a [bindings] table in TOML.
    /// Keyed by string (Operation.ToString()), not the enum itself - Tomlyn 2.x only supports
    /// string-keyed dictionaries as TOML tables. Each value can contain multiple alternatives
    /// separated by semicolons (e.g. "F5;Ctrl+C").
    ///
    /// Deliberately holds *only* what the user has explicitly customized - never the resolved
    /// defaults. RebuildNormalized merges GetDefaults(style) with this purely at runtime, without
    /// writing the merge result back here, so the on-disk file (and this property, since it's what
    /// gets serialized) only ever grows when the user actually overrides something. RestoreDefaults
    /// clears it back to empty.
    /// </summary>
    [Tomlyn.Serialization.TomlPropertyName("bindings")]
    public Dictionary<string, string> Bindings { get; set; } = new();

    /// <summary>
    /// Reverse map: normalized key gesture -> Operation, rebuilt at runtime after (de)serialization
    /// or after RestoreDefaults/a keyboard style change.
    /// </summary>
    private Dictionary<KeyGesture, Operation> MapKeyToOp { get; set; } = new();

    /// <summary>
    /// Forward map: Operation -> every key gesture currently bound to it, in the order they appear
    /// in the effective (defaults + user overrides) binding string. Rebuilt in the same pass as
    /// MapKeyToOp - one merge, two views of the same data. See GetPrimaryGesture for what this is
    /// actually for.
    /// </summary>
    private Dictionary<Operation, List<KeyGesture>> MapOpToGestures { get; set; } = new();

    /// <summary>
    /// Built-in bindings for a given keyboard style, matching Total Commander conventions where
    /// applicable. Only used to fill gaps for operations the user hasn't configured in Bindings -
    /// user values always win over these, per operation.
    ///
    /// The Windows and macOS sets are identical except that Ctrl becomes Meta (Cmd) throughout -
    /// with one deliberate exception: SwitchTabInSamePanel stays Ctrl+Tab even in the macOS set,
    /// because Cmd+Tab is macOS's own system-wide app switcher (a true OS-level reservation, the
    /// same class of problem GlobalShortcutGuard/MacReservedCombos exists for) and remapping it is
    /// unreliable even with an accessibility-level hook. Every other Ctrl+ binding here only
    /// collides with app-level conventions (e.g. Cmd+M = minimize), which don't intercept before
    /// delivery, so CatCommander simply receives them normally.
    /// </summary>
    public static Dictionary<Operation, string> GetDefaults(KeyboardStyle style)
    {
        var primaryModifier = style == KeyboardStyle.MacOS ? "Meta" : "Ctrl";

        return new Dictionary<Operation, string>
        {
            [Operation.Copy] = "F5",
            [Operation.Move] = "F6",
            [Operation.Rename] = "Shift+F6;F2",
            [Operation.Delete] = "F8;Delete",
            [Operation.CreateDirectory] = "F7",
            [Operation.ExpandCurrentFolder] = $"{primaryModifier}+B",
            [Operation.ExpandSelectedFolders] = $"{primaryModifier}+Shift+B",
            [Operation.GoIntoCurrentFolder] = "Enter;Right",
            [Operation.GoBackToParentFolder] = "Left",
            [Operation.GotoFirstItem] = "Home",
            [Operation.GotoLastItem] = "End",
            [Operation.ReverseSelection] = "Alt+R",
            [Operation.Refresh] = $"{primaryModifier}+R",
            [Operation.ToggleHiddenFiles] = $"{primaryModifier}+.",
            [Operation.OpenSelectedFolderInNewTab] = $"{primaryModifier}+Up",
            [Operation.SwitchTabInSamePanel] = "Ctrl+Tab",
            [Operation.SwitchPanel] = "Tab",
            [Operation.CloseTab] = $"{primaryModifier}+W",
            [Operation.OpenCurrentFolderInOppositePanel] = $"{primaryModifier}+Left;{primaryModifier}+Right",
            [Operation.OpenFind] = "Alt+F7",
            [Operation.OpenBatchRename] = $"{primaryModifier}+M",
        };
    }

    /// <summary>
    /// Merges Bindings (user overrides) over GetDefaults(style) and rebuilds the reverse lookup
    /// map. Does *not* mutate Bindings - the merge result only lives in the runtime lookup map.
    /// Call after loading from TOML, after RestoreDefaults, or after the keyboard style changes.
    /// </summary>
    public void RebuildNormalized(KeyboardStyle style)
    {
        var effective = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (op, keys) in GetDefaults(style))
            effective[op.ToString()] = keys;
        foreach (var (opName, keys) in Bindings)
        {
            if (!string.IsNullOrWhiteSpace(keys))
                effective[opName] = keys;
        }

        var map = new Dictionary<KeyGesture, Operation>();
        var opToGestures = new Dictionary<Operation, List<KeyGesture>>();
        foreach (var (opName, keysString) in effective)
        {
            if (!Enum.TryParse<Operation>(opName, out var operation))
            {
                log.Warn($"unknown operation '{opName}' in shortcut bindings, skipping");
                continue;
            }

            var alternatives = keysString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var keyStr in alternatives)
            {
                KeyGesture gesture;
                try
                {
                    gesture = KeyGesture.Parse(keyStr);
                }
                catch (Exception ex)
                {
                    log.Warn(ex, $"cannot parse shortcut '{keyStr}' for {operation}, skipping");
                    continue;
                }

                if (map.TryGetValue(gesture, out var existing) && existing != operation)
                    log.Warn($"shortcut conflict: {gesture} was bound to {existing}, rebinding to {operation}");

                map[gesture] = operation;

                if (!opToGestures.TryGetValue(operation, out var gestures))
                    opToGestures[operation] = gestures = new List<KeyGesture>();
                gestures.Add(gesture);
            }
        }

        MapKeyToOp = map;
        MapOpToGestures = opToGestures;
    }

    /// <summary>
    /// Clears all user customizations, reverting to whatever GetDefaults(style) produces. Caller
    /// is responsible for calling RebuildNormalized(style) and persisting afterward.
    /// </summary>
    public void RestoreDefaults() => Bindings.Clear();

    /// <summary>
    /// Gets the operation mapped to a given key gesture, or Operation.Nop if unbound.
    /// </summary>
    public Operation GetOperation(KeyGesture gesture) => MapKeyToOp.GetValueOrDefault(gesture, Operation.Nop);

    /// <summary>
    /// Gets the operation mapped to a raw Key + KeyModifiers pair, as delivered by KeyEventArgs.
    /// </summary>
    public Operation GetOperation(Key key, KeyModifiers modifiers) => GetOperation(new KeyGesture(key, modifiers));

    /// <summary>
    /// The primary (first-listed) key gesture currently bound to an Operation, after merging user
    /// overrides over the defaults - used to set macOS NativeMenuItem.Gesture from code (see
    /// MainWindow.axaml.cs) instead of a hardcoded XAML string, so the menu's displayed/native-
    /// active shortcut can never drift from what a real keystroke would actually dispatch through
    /// ShortcutRouter. Null if the operation currently has no gesture bound at all.
    /// </summary>
    public KeyGesture? GetPrimaryGesture(Operation operation) =>
        MapOpToGestures.TryGetValue(operation, out var gestures) && gestures.Count > 0 ? gestures[0] : null;
}
