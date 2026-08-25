# Keyboard Shortcuts

See [README.md](README.md) for the app overview and architecture; this file is just the keybinding
reference.

CatCommander's default keymap is hardcoded per OS - there is no in-app setting to pick "Windows
style" vs. "macOS style"; the app simply looks at which OS it's running on
(`ShortcutsSettings.CurrentStyle`) and uses the matching table below. Windows and Linux share the
same defaults.

You can override any binding by adding it to `Config/keymap.toml` under `[bindings]`, e.g.:

```toml
[bindings]
Copy = "Ctrl+Shift+C"
```

Multiple alternative gestures for the same operation are separated by `;` (see `Rename` below for
an example). A user override *replaces* the default for that operation rather than adding to it.

To wipe out all of your customizations and go back to the table below, use
**Settings > Restore Default Shortcuts** in the app menu - this clears `[bindings]` in
`keymap.toml` entirely.

| Operation | Windows / Linux | macOS |
|---|---|---|
| Copy | `F5` | `F5` |
| Move | `F6` | `F6` |
| Rename | `Shift+F6`, `F2` | `Shift+F6`, `F2` |
| Delete | `F8`, `Delete` | `F8`, `Delete` |
| Expand current folder | `Ctrl+B` | `Cmd+B` |
| Expand selected folders | `Ctrl+Shift+B` | `Cmd+Shift+B` |
| Go into current folder | `Enter`, `Right` | `Enter`, `Right` |
| Go back to parent folder | `Left` | `Left` |
| Go to first item | `Home` | `Home` |
| Go to last item | `End` | `End` |
| Reverse selection | `Alt+R` | `Alt+R` |
| Open selected folder in new tab | `Ctrl+Up` | `Cmd+Up` |
| Switch tab in same panel | `Ctrl+Tab` | `Ctrl+Tab` |
| Switch panel | `Tab` | `Tab` |
| Close tab | `Ctrl+W` | `Cmd+W` |
| Open current folder in opposite panel | `Ctrl+Left`, `Ctrl+Right` | `Cmd+Left`, `Cmd+Right` |
| Find | `Alt+F7` | `Alt+F7` |
| Batch rename | `Ctrl+M` | `Cmd+M` |

`Switch tab in same panel` stays `Ctrl+Tab` on macOS rather than becoming `Cmd+Tab`, because
`Cmd+Tab` is macOS's own system-wide app switcher - a true OS-level reservation, not just an app
convention - and remapping it isn't reliable even with an accessibility-level hook.

`Ctrl+Tab` itself is *also* reserved by macOS (AppKit's "next window tab" shortcut, even though
CatCommander has no native tab group to cycle) - without patching, AppKit consumes the Control
modifier before delivery and the app only ever sees a bare `Tab` keystroke. `GlobalShortcutGuard`
(see README.md) intercepts it via a lower-level SharpHook hook instead of Avalonia's normal input
pipeline, the same way it does for the Mission Control combos below.

`Close tab` removes the active tab and activates a sibling (the one to its left, or to its right if
it was the first tab). If it's the panel's only tab, closing it doesn't remove it - a panel always
keeps at least one tab, so the tab is reset to the Home folder in place instead.

`Open current folder in opposite panel` opens the *selected* folder (not the directory the active
tab is currently browsing) in a new tab in the *other* panel - exactly `Open selected folder in new
tab`'s own logic, aimed across panels instead of within one. Both `Left` and `Right` trigger the
same action, since "opposite panel" already means whichever one isn't active; there's no direction
left for the two keys to disambiguate.

`Reverse selection` flips the marked state of every currently *visible* row (see Multi-selection
below) - a row hidden by the quick filter is left alone, since marking it would violate "marked is
always a subset of visible".

This table is generated from `ShortcutsSettings.GetDefaults` in
`CatCommander/Config/ShortcutsSettings.cs` - if you change the defaults there, update this table
to match.

## Multi-selection

`Space` toggles the marked state of whatever row is currently under the cursor - Total Commander's
checkbox-style multi-selection, shown as red text, and deliberately separate from the cursor itself
(which still moves on arrow keys/click and is unaffected by marking). The status bar's `Selected
X / Y` figures are the sum of every *marked* row, not just the one under the cursor.

`Copy`, `Move`, and `Delete` act on every marked row if any are marked, otherwise on whatever's
under the cursor - so a single-item operation never requires marking first. `Rename` doesn't follow
this rule: renaming several items at once needs a pattern, which is what `Batch rename` is for.

Marks are never remembered across a directory change (entering a folder, going back, jumping via
history, ...) - a fresh listing always starts with nothing marked. They *are* kept per tab, though:
switching to another tab and back leaves a tab's marks exactly as you left them. A row that a quick
filter hides is unmarked at the moment it's hidden (see "marked is always a subset of visible"
above) - widening or changing the filter afterward does not bring an old mark back.

## Quick filter

Not in the table above - it's typed text, not a remappable Operation. Typing any character while
a panel's grid has focus opens a filter bar below the grid and narrows the listing to items whose
name contains what you've typed (case-insensitive). Separate multiple words with a space to AND
them together - `aa bb` matches `aaccbb` but not `aacc` or `bbcc` alone. `Backspace` edits the
filter one character at a time; `Escape` clears it and shows the folder's full contents again.

`Space` is the one exception: while a filter is already active it's a word separator like any other
typed character, but with no filter active yet it doesn't start one - it toggles a mark instead (see
Multi-selection above).
