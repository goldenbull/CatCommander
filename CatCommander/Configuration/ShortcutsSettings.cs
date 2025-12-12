using System;
using System.Collections.Generic;
using CatCommander.Commands;
using NLog;

namespace CatCommander.Configuration;

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
        Bindings = new();
        Bindings[Operation.Copy] = "Ctrl+C";
        Bindings[Operation.Move] = "F5";
        Bindings[Operation.Rename] = "Shift+F6;F2";
        Bindings[Operation.Delete] = "F8;Delete";
        Bindings[Operation.ExpandCurrentFolder] = "Right";
        Bindings[Operation.ExpandSelectedFolders] = "Ctrl+Right";
        Bindings[Operation.GoIntoCurrentFolder] = "Enter;Ctrl+Down";
        Bindings[Operation.GoBackToParentFolder] = "Backspace;Ctrl+Up";
        Bindings[Operation.GotoFirstItem] = "Home;Ctrl+Home";
        Bindings[Operation.GotoLastItem] = "End;Ctrl+End";

        RebuildReverseMap();
    }

    /// <summary>
    /// Rebuilds the reverse map
    /// Should be called after loading from TOML or modifying shortcuts
    /// </summary>
    public void RebuildReverseMap()
    {
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
                if (keyEvt == null) continue;
                
                // If key already exists, last one wins with warning
                if (MapKeyToOp.TryGetValue(keyEvt, out var value))
                {
                    log.Warn($"overwrite {keyEvt}, {value} --> {operation}");
                }

                MapKeyToOp[keyEvt] = operation;
            }
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