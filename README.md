# CatCommander

A cross-platform (Windows / Linux / macOS) dual-pane file manager in the tradition of Total
Commander, built with Avalonia UI. Keyboard-first: almost everything is reachable without a mouse.

Keyboard shortcut reference: see [shortcuts.md](shortcuts.md).

## Project layout

| Project | Purpose |
|---|---|
| `libcat` | Platform-agnostic core: file system abstraction (`IFileSystemProvider`), models, quick-access list. No Avalonia reference - keeps UI types out of the data layer. |
| `CatCommander` | The Avalonia UI app: Views, ViewModels, config, keyboard shortcut routing, theming. |
| `Avalonia.Controls.TreeDataGrid` | Vendored, not a NuGet package. 11.2.0+ requires a paid Avalonia Accelerate license (`AVLIC0001` at build time); this is the last MIT-licensed commit, patched in place. See its `NOTICE.md`. |
| `libcat.Tests` | xUnit, pure unit tests against `libcat` - no Avalonia dependency, fast. |
| `CatCommander.Tests` | xUnit + `Avalonia.Headless` - real (simulated) window/keyboard-driven tests against `CatCommander`. |

No solution file; build/test each project directly (`dotnet build CatCommander/CatCommander.csproj`,
`dotnet test CatCommander.Tests/CatCommander.Tests.csproj`, etc.).

## Architecture

### Composition root, not singletons

`Program.cs` wires everything through `Microsoft.Extensions.DependencyInjection` - `MainWindowViewModel`,
`MainPanelViewModel`, `ItemBrowserViewModel`, `ConfigManager`, `FileSystemProviderRegistry`, `IconCache`
are all constructor-injected. `MainPanelViewModel`/`ItemBrowserViewModel` need more than one instance
of the same type (left/right panel, per-tab browsers), so they're resolved via `Func<T>` factories
rather than injected directly - DI containers don't auto-synthesize those.

### View/ViewModel chain

```
MainWindowViewModel
├── LeftPanel: MainPanelViewModel
│     └── Tabs: ItemBrowserViewModel[]  (one per open tab)
└── RightPanel: MainPanelViewModel
      └── Tabs: ItemBrowserViewModel[]
```

`FileItemRow` (in `CatCommander/ViewModels`) wraps `libcat`'s `IFileSystemItem` for the grid,
adding an async-loaded `Avalonia.Media.Imaging.Bitmap` icon - this is deliberately the *only*
place a UI type leaks in; `libcat` itself stays Avalonia-free.

### Keyboard shortcuts

This is the part of the app that took the most iteration to get right, so it's documented in
detail here rather than left to be re-discovered.

**The pieces, in `CatCommander/Config` and `CatCommander/Shortcuts`:**

- **`Operation`** (enum) - one value per keyboard-triggerable action. Both the TOML config keys
  and the dispatch target names.
- **`ShortcutsSettings`** - `GetDefaults(KeyboardStyle)` returns the hardcoded default keymap
  (Ctrl on Windows/Linux, Cmd on macOS, with named exceptions - see `shortcuts.md`).
  `RebuildNormalized` merges the user's `[bindings]` overrides (from `keymap.toml`) on top of the
  defaults into a runtime `KeyGesture -> Operation` lookup map. `ShortcutsSettings.CurrentStyle` is
  derived from `OperatingSystem.IsMacOS()` - the OS choice is hardcoded, not a user-facing setting;
  the only shortcut-related UI is "Restore Default Shortcuts" (clears `[bindings]`).
- **`IShortcutCommandSource`** - `ICommand? GetCommand(Operation)`. Implemented by
  `MainWindowViewModel`, `MainPanelViewModel`, and `ItemBrowserViewModel`. Dispatch is a chain of
  responsibility: window-level commands first, then the active panel's own commands (things that
  need the whole `Tabs` collection, like opening a new tab), then the active tab's commands
  (navigation). Nobody but `MainWindowViewModel` needs to know panels or tabs exist.
- **`ShortcutRouter`** - installed once per `Window`. A Tunnel-phase `KeyDown` handler that looks
  up the `Operation` for the pressed gesture and dispatches through `IShortcutCommandSource`,
  unless `TextEditKeyExceptions` says the focused control (a `TextBox`) should keep the keystroke
  for normal text editing instead (e.g. plain `Left`/`Right`/`Ctrl+C` while typing a path).
- **`GlobalShortcutGuard`** + **`MacReservedCombos`** - macOS-only. A small set of gestures never
  reach `ShortcutRouter` at all because macOS/AppKit consumes them before delivery to any app's
  normal input pipeline (see "AppKit eats some key combos" below). `GlobalShortcutGuard` uses
  SharpHook's `SimpleGlobalHook` to intercept those *specific* gestures at the OS level, suppress
  the event, and dispatch through the same `IShortcutCommandSource` chain. It only acts on gestures
  in `MacReservedCombos` - everything else flows through `ShortcutRouter` normally.

### Keyboard focus

`ItemBrowserViewModel.FocusRequested` (an event) is the single source of truth for "real Avalonia
keyboard focus should be on this tab's grid right now." It's raised whenever `RebuildSource()` runs
(every navigation, every view-mode toggle) and whenever a panel is handed activation
(`MainPanelViewModel.RequestFocus`, called by `MainWindowViewModel.SwitchPanel`). The View
(`ItemBrowser.axaml.cs`) subscribes to it and is the only place that actually calls `.Focus()`.

Two things about `MainWindowViewModel.SetActivePanel` matter and are easy to get wrong again if
this code is touched:

- **Reactive vs. commanded activation are different methods.** `SetActivePanel` (called from
  `MainPanel`'s `GotFocus` handler - a mouse click, or the echo of our own `RequestFocus`) only
  records state; it must never itself call `RequestFocus()`. `SwitchPanel` (Tab key) is the only
  path that both records state *and* pushes focus. Merging these two directions caused a genuine
  infinite `GotFocus` → `RequestFocus` → `GotFocus` ping-pong between the two panels at startup -
  see "lessons learned" below.
- Focus-pushing in `ItemBrowser.axaml.cs` is always deferred via `Dispatcher.UIThread.Post`, never
  called synchronously from within a `KeyDown` dispatch - Avalonia's own Tab-key focus navigation
  isn't suppressed by `e.Handled`, so a synchronous `Focus()` call inside the same dispatch loses
  to it.

### Theming

`CatCommander/Styles/ClassicTheme.axaml`, loaded after `FluentTheme` in `App.axaml`, repaints the
whole app to a fixed classic-Total-Commander palette (`RequestedThemeVariant="Light"` - no
dark-mode variant to maintain). 12pt font, square corners, thin gray borders. The active/inactive
panel distinction is done via a `Classes.active` binding threaded from `MainPanelViewModel.IsActive`
down into `ItemBrowser`'s `TreeDataGrid`/`TextBox` (using an ancestor-lookup binding, since
`ItemBrowser`'s own `DataContext` is the per-tab `ItemBrowserViewModel`, not the panel) - the
active panel's address bar and selection go navy, the inactive one's selection goes muted gray.

## Testing strategy - and its limits

- `libcat.Tests`: plain xUnit, no UI.
- `CatCommander.Tests`: `Avalonia.Headless` + `Avalonia.Headless.XUnit`. Tests build a real
  `MainWindow`/`MainWindowViewModel` and drive it with simulated keyboard input
  (`window.KeyPress(...)`), then assert on *actual* Avalonia state (`control.IsFocused`, selection
  model contents) - not just that a ViewModel method ran. `MainWindowFocusTests.cs` is the main
  example; read its comments before changing focus-related code.

**Headless tests cannot catch OS-level input interception.** Several of the keyboard bugs fixed in
this app's history only reproduced in the real, running desktop app - headless simulation delivers
`KeyDown` events directly into Avalonia's input pipeline, bypassing whatever the real OS does to a
keystroke before Avalonia ever sees it. When a keyboard shortcut behaves correctly in
`CatCommander.Tests` but not for a real user, check whether the OS is intercepting it first (see
below) before assuming a code regression.

## Lessons learned

Concrete things that cost real debugging time, kept here so they don't get re-discovered:

1. **`TreeDataGrid` is not `Focusable` by default - only its cells are.** Calling `.Focus()` on the
   `TreeDataGrid` control itself is a silent no-op (no exception) unless `Focusable="True"` is set
   explicitly (see `ItemBrowser.axaml`). This made every early attempt at fixing keyboard focus
   look like a timing bug when it was actually targeting a control that could never receive focus.
2. **AppKit eats some key combos before Avalonia's input pipeline sees them, and it doesn't always
   look like it.** `Cmd+Left/Right/Up/Down`/`F3` (Mission Control) and `Cmd+Tab` (app switcher) are
   the obvious ones. Less obvious: **`Ctrl+Tab` is AppKit's "next window tab" shortcut** even for a
   single-window app with no native tab group - macOS still consumes the Control modifier and
   passes through a bare `Tab` keystroke, which looks exactly like a plain Tab press in logs. If a
   real macOS user reports a `Ctrl+`/`Cmd+`-modified shortcut "doing the unmodified version
   instead," suspect this before anything else. Confirmed reservations go in `MacReservedCombos`
   and get patched via `GlobalShortcutGuard`.
3. **Avalonia's own Tab-key focus navigation ignores `e.Handled`.** It's an accessibility
   guarantee, not something app code can suppress by marking the event handled in a Tunnel-phase
   handler. Any focus correction triggered by a Tab-bound shortcut must be deferred to run *after*
   the whole key-down dispatch (including Avalonia's own navigation) has settled, not called
   synchronously from within it.
4. **A reactive focus-tracking handler must never re-trigger the action that reacts to it.**
   `MainPanel`'s `GotFocus` → `SetActivePanel` used to also call `RequestFocus()` "to be safe."
   With two panels each independently self-focusing at startup, that turned into a real infinite
   `GotFocus → RequestFocus → GotFocus` loop that hung the app. Reactive handlers record state;
   only an explicit command (a keypress, a click) should push focus.
5. **`TreeDataGrid`'s Star-width column recompute has its own gating bug** for a grid attached
   after the window is already laid out and stable in size (`ColumnList.ViewportChanged` only
   recomputes when the viewport width actually *changes* from what it last saw - which it won't,
   for a new tab in an already-sized window). `IColumns.CommitActualWidths()` bypasses that gate,
   but only produces a correct result once a real layout pass has actually measured the grid's
   cells - trigger it from `Layoutable.LayoutUpdated` (a real "a layout pass just finished" signal),
   not a guessed `DispatcherPriority` or a fixed number of `Post` ticks.
6. **Verify you're testing the build you think you're testing.** A stale pre-built `.app` bundle
   (from `build-macos-app.sh`, registered with Launch Services under the app's name) can get
   activated by AppleScript's `tell application "CatCommander"` in preference to a bare `dotnet run`
   process, silently testing old code. When in doubt, run from source and check the NLog console
   output for evidence the code path you expect actually ran.

## Logging

`CatCommander/NLog.config` (auto-discovered by NLog from the app's base directory - no explicit
setup call) logs to both the console and `~/Library/Application Support/CatCommander/logs/` (or
the equivalent on other platforms) at Debug level. `ShortcutRouter` and `GlobalShortcutGuard` both
log every dispatched shortcut - the first thing to check when a shortcut "does nothing" is whether
it's being dispatched at all.

## Build / run / test

```bash
dotnet build CatCommander/CatCommander.csproj
dotnet run --project CatCommander/CatCommander.csproj
dotnet test CatCommander.Tests/CatCommander.Tests.csproj
dotnet test libcat.Tests/libcat.Tests.csproj
```
