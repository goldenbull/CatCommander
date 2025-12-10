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
    /// Forward map: Operation -> Key bindings (loaded from TOML)
    /// Each value can contain multiple alternatives separated by semicolons (e.g., "F5;Ctrl+C")
    /// </summary>
    public Dictionary<Operation, string> MapOpToKey { get; }

    /// <summary>
    /// Reverse map: Normalized keystroke -> Operation (built at runtime)
    /// Used for fast keystroke lookup during keyboard event handling
    /// </summary>
    private Dictionary<string, Operation> MapKeyToOp { get; set; }

    public ShortcutsSettings()
    {
        // Set default shortcuts matching Total Commander conventions
        MapOpToKey = new();
        MapOpToKey[Operation.Copy] = "Ctrl+C";
        MapOpToKey[Operation.Move] = "F5";
        MapOpToKey[Operation.Rename] = "Shift+F6;F2";
        MapOpToKey[Operation.Delete] = "F8;Delete";
        MapOpToKey[Operation.ExpandCurrentFolder] = "Right";
        MapOpToKey[Operation.ExpandSelectedFolders] = "Ctrl+Right";
        MapOpToKey[Operation.GoIntoCurrentFolder] = "Enter;Ctrl+Down";
        MapOpToKey[Operation.GoBackToParentFolder] = "Backspace;Ctrl+Up";
        MapOpToKey[Operation.GotoFirstItem] = "Home;Ctrl+Home";
        MapOpToKey[Operation.GotoLastItem] = "End;Ctrl+End";

        MapKeyToOp = new();
        RebuildReverseMap();
    }

    /// <summary>
    /// Rebuilds the reverse map (MapKey2Op) from the forward map (MapOp2Key)
    /// Should be called after loading from TOML or modifying shortcuts
    /// </summary>
    public void RebuildReverseMap()
    {
        MapKeyToOp = new Dictionary<string, Operation>();

        foreach (var (operation, keysString) in MapOpToKey)
        {
            if (string.IsNullOrWhiteSpace(keysString))
                continue;

            // Split by semicolon to get alternative key bindings
            var alternatives = keysString.Split(';', StringSplitOptions.RemoveEmptyEntries
                                                   | StringSplitOptions.TrimEntries);
            foreach (var keyStr in alternatives)
            {
                // Normalize the keystroke for consistent lookup
                var normalizedKeyStr = KeyboardHookManager.Normalized(keyStr);
                if (!string.IsNullOrWhiteSpace(normalizedKeyStr))
                {
                    // If key already exists, last one wins with warning
                    if (MapKeyToOp.TryGetValue(normalizedKeyStr, out var value))
                    {
                        log.Warn($"overwrite {normalizedKeyStr}, {value} --> {operation}");
                    }
                    MapKeyToOp[normalizedKeyStr] = operation;
                }
            }
        }
    }

    /// <summary>
    /// Gets the operation mapped to a given keystroke
    /// </summary>
    /// <param name="normalizedKey">The keystroke to look up (must already be normalized)</param>
    /// <returns>The mapped operation, or Nop if not found</returns>
    public Operation GetOperation(string normalizedKey)
    {
        return MapKeyToOp.GetValueOrDefault(normalizedKey, Operation.Nop);
    }
}