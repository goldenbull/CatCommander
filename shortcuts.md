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
| Create directory | `F7` | `F7` |
| Expand current folder | `Ctrl+B` | `Cmd+B` |
| Expand selected folders | `Ctrl+Shift+B` | `Cmd+Shift+B` |
| Go into current folder | `Enter`, `Right` | `Enter`, `Right` |
| Go back to parent folder | `Left` | `Left` |
| Go to first item | `Home` | `Home` |
| Go to last item | `End` | `End` |
| Reverse selection | `Alt+R` | `Alt+R` |
| Refresh | `Ctrl+R` | `Cmd+R` |
| Toggle hidden files | `Ctrl+.` | `Cmd+.` |
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

## Copy / Move / Delete

`F5`/`F6`/`F8` (or `Delete`) first show a small confirmation dialog naming the target count -
Copy/Move also show the destination, always the *opposite* panel's own current directory, not
editable (unlike Total Commander's dialog, there's no destination path field); Delete has no
destination to show, just an "this cannot be undone" warning. It offers three choices:

- **Copy**/**Move**/**Delete** - runs the job now, in a modal progress dialog that blocks the main
  window while it tracks the job live. Its own **Send to Background** button closes the dialog
  without cancelling the job - it just keeps running, now only visible via **File Operations**
  below.
- **Background** - queues the job and returns control to the main window immediately, with no
  progress dialog at all.
- **Cancel** - does nothing.

Either way, every Copy/Move/Delete job runs through one shared, serial background queue -
"blocking" vs. "background" is only how the UI presents an already-running job, never a different
execution path. **File > File Operations...** (also on the toolbar) opens a non-modal window
listing every job ever queued this session, each with a live progress bar - not just the ones
started in Background mode.

A name collision at the destination is overwritten, not skipped or prompted for - jobs run
unattended on the queue, where there's no one to prompt.

## Double-click

Not in the table above - it's a mouse gesture, not a remappable Operation. Double-clicking a
directory enters it, exactly like `Go into current folder`. Double-clicking a file hands it to the
OS's own default handler for that file type (Finder/Explorer's own double-click behavior) - unlike
`Enter`/`Right`, which never launch an external app, since those double as ordinary keyboard
navigation.

## In-place rename

`F2`'s edit happens directly in the grid cell, not a dialog: the name becomes an editable text box,
pre-selecting the base filename (excluding the extension) for a file, or the whole name for a
directory. `Enter` commits, `Escape` discards the edit, and clicking away commits it too (Explorer/
Total Commander convention). An empty or unchanged name is a no-op - it just closes the box without
touching the file system.

## Quick filter

Not in the table above - it's typed text, not a remappable Operation. Typing any character while
a panel's grid has focus opens a filter bar below the grid and narrows the listing to items whose
name contains what you've typed (case-insensitive). Separate multiple words with a space to AND
them together - `aa bb` matches `aaccbb` but not `aacc` or `bbcc` alone. `Backspace` edits the
filter one character at a time; `Escape` clears it and shows the folder's full contents again.

`Space` is the one exception: while a filter is already active it's a word separator like any other
typed character, but with no filter active yet it doesn't start one - it toggles a mark instead (see
Multi-selection above).

## Hidden files

`Toggle hidden files` starts off, hiding dotfiles on every platform and, on Windows, anything with
the OS Hidden attribute set too (a Windows file can be hidden either way; macOS/Linux only ever use
the dot prefix). Unlike the quick filter, this is a per-tab preference that survives navigating to a
new folder - it isn't reset the way `FilterText` is on every fresh listing. Tree-list mode's
expanded folders respect it too, not just the top-level listing.
