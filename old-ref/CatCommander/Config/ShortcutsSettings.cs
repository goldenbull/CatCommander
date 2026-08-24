using System;
using System.Collections.Generic;
using System.Linq;
using CatCommander.Commands;
using NLog;

namespace CatCommander.Config;

// use enums as the identifier for both keys in config file and commands in CommandExecutor.cs
public enum Operation
{
    Nop, // special non-op, to avoid null return
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
    SwitchPanel
}

public class ShortcutsSettings
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Forward map: Operation -> Key bindings in string format (loaded from TOML)
    /// Each value can contain multiple alternatives separated by semicolons (e.g., "F5;Ctrl+C")
    /// </summary>
    public Dictionary<Operation, string> Bindings { get; }

    /// <summary>
    /// Reverse map: Normalized keystroke -> Operation (built at runtime)
    /// Used for fast keystroke lookup during keyboard event handling
    /// </summary>
    private Dictionary<CatKeyEventArgs, Operation> MapKeyToOp { get; set; } = new();

    public ShortcutsSettings()
    {
        // Set default shortcuts matching Total Commander conventions
        Bindings = new()
        {
            [Operation.Copy] = "Ctrl+C",
            [Operation.Move] = "F5",
            [Operation.Rename] = "Shift+F6;F2",
            [Operation.Delete] = "F8;Delete",
            [Operation.ExpandCurrentFolder] = "Ctrl+B",
            [Operation.ExpandSelectedFolders] = "Ctrl+Shift+B",
            [Operation.GoIntoCurrentFolder] = "Enter;Right",
            [Operation.GoBackToParentFolder] = "Left",
            [Operation.GotoFirstItem] = "Home",
            [Operation.GotoLastItem] = "End"
        };

        RebuildNormalized();
    }

    /// <summary>
    /// Rebuilds the reverse map and then update binding map
    /// Should be called after loading from TOML or modifying shortcuts
    /// </summary>
    public void RebuildNormalized()
    {
        // rebuild key --> op map from binding map
        MapKeyToOp.Clear();

        foreach (var (operation, keysString) in Bindings)
        {
            if (string.IsNullOrWhiteSpace(keysString))
                continue;

            // Split by semicolon to get alternative key bindings
            var alternatives = keysString.Split(';', StringSplitOptions.RemoveEmptyEntries
                                                     | StringSplitOptions.TrimEntries);
            foreach (var keyStr in alternatives)
            {
                // Normalize the keystroke for consistent lookup
                var keyEvt = KeyboardHookManager.Parse(keyStr);
                if (keyEvt == CatKeyEventArgs.Empty) continue;

                // If key already exists, last one wins with warning
                if (MapKeyToOp.TryGetValue(keyEvt, out var value) && operation != value)
                {
                    log.Warn($"overwrite {keyEvt}, {value} --> {operation}");
                }

                MapKeyToOp[keyEvt] = operation;
            }
        }

        // apply pre-defined shortcuts
        var predefined = new Dictionary<CatKeyEventArgs, Operation>()
        {
            [KeyboardHookManager.Parse("right")] = Operation.GoIntoCurrentFolder,
            [KeyboardHookManager.Parse("left")] = Operation.GoBackToParentFolder,
            [KeyboardHookManager.Parse("home")] = Operation.GotoFirstItem,
            [KeyboardHookManager.Parse("end")] = Operation.GotoLastItem,
            [KeyboardHookManager.Parse("ctrl+tab")] = Operation.SwitchTabInSamePanel,
            [KeyboardHookManager.Parse("tab")] = Operation.SwitchPanel,
        };

        foreach (var (keyEvt, operation) in predefined)
        {
            if (MapKeyToOp.TryGetValue(keyEvt, out var value) && operation != value)
            {
                log.Warn($"prefined shortcuts overwrite {keyEvt}, {value} --> {operation}");
            }

            MapKeyToOp[keyEvt] = operation;
        }

        // update binding dict from key->op map to elimination conflictions
        Bindings.Clear();
        foreach (var op in MapKeyToOp.Values.Distinct())
        {
            var keys = MapKeyToOp.Where(kvp => kvp.Value == op)
                .Select(kvp => kvp.Key.ToString())
                .ToList();
            Bindings[op] = string.Join(";", keys);
        }
    }

    /// <summary>
    /// Gets the operation mapped to a given keystroke
    /// </summary>
    /// <param name="keyEvt">The keystroke to look up (must already be normalized)</param>
    /// <returns>The mapped operation, or Nop if not found</returns>
    public Operation GetOperation(CatKeyEventArgs keyEvt)
    {
        return MapKeyToOp.GetValueOrDefault(keyEvt, Operation.Nop);
    }
}