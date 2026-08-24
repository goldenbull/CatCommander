using System;
using System.Collections.Generic;
using Avalonia.Input;
using NLog;

namespace CatCommander.Config;

// Identifiers for both keys in the TOML config file and commands dispatched by IShortcutCommandSource.
public enum Operation
{
    Nop, // non-op, so lookups can return a value instead of null
    Copy,
    Move,
    Rename,
    Delete,
    ExpandCurrentFolder,
    ExpandSelectedFolders,
    GoIntoCurrentFolder,
    GoBackToParentFolder,
    GotoFirstItem,
    GotoLastItem,
    SwitchTabInSamePanel,
    SwitchPanel,
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
            [Operation.ExpandCurrentFolder] = $"{primaryModifier}+B",
            [Operation.ExpandSelectedFolders] = $"{primaryModifier}+Shift+B",
            [Operation.GoIntoCurrentFolder] = "Enter;Right",
            [Operation.GoBackToParentFolder] = "Left",
            [Operation.GotoFirstItem] = "Home",
            [Operation.GotoLastItem] = "End",
            [Operation.SwitchTabInSamePanel] = "Ctrl+Tab",
            [Operation.SwitchPanel] = "Tab",
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
            }
        }

        MapKeyToOp = map;
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
}
