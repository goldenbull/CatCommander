# Provenance

Vendored from https://github.com/AvaloniaUI/Avalonia.Controls.TreeDataGrid

- Commit: `0cb3b3a5cba5efb1da0477694a4636ca680abf08` (2025-01-30, "bump version")
- This is the exact commit the `Avalonia.Controls.TreeDataGrid` 11.1.1 NuGet package
  was built from (per that package's `.nuspec` `<repository>` element) - the last
  version released under the MIT license before AvaloniaUI moved the control behind
  the paid Avalonia Accelerate (Pro/Enterprise) license starting with 11.2.0.
- Only `src/Avalonia.Controls.TreeDataGrid/` was copied (the actual control code).
  Samples, tests, and the upstream repo's Nuke/Azure Pipelines build infrastructure
  were left behind.
- License: MIT, see `LICENSE.md` in this folder (copied unchanged from upstream's
  `licence.md`).

## Why vendored instead of a NuGet reference

11.1.1 is the last free release and will not receive further updates from AvaloniaUI
- there is no newer MIT version to ever upgrade to via NuGet. Vendoring lets us patch
  bugs directly in the source when we hit them, since upstream won't.

## Modifications from upstream

- `Avalonia.Controls.TreeDataGrid.csproj` retargeted from `net5.0` to `net10.0`;
  dropped `PackageId`/pack metadata (`IsPackable=false`) since this isn't meant to be
  published as a NuGet package itself.
- No changes to any `.cs`/`.axaml` file content.

## Upgrading

There is nothing to pull forward from upstream on the MIT line - this is a permanent
fork. If a bug is fixed here, note it below so future re-vendoring (should AvaloniaUI
ever re-license, or should we fork from a different point) doesn't lose the fix.

### Local fixes applied

- **Row drag/drop ported from the obsolete `IDataObject`/`DataObject`/`DragDrop.DoDragDrop` API to
  the current `IDataTransfer`/`DataTransfer`/`DragDrop.DoDragDropAsync` API** (`TreeDataGrid.cs`,
  `Models/TreeDataGrid/DragInfo.cs`). The obvious 1:1 replacement - a custom
  `DataFormat<DragInfo>` created via `DataFormat.FromSystemName<T>` - doesn't compile: that overload
  carries `[Avalonia.Metadata.PrivateApiAttribute]` and isn't visible outside Avalonia's own
  assemblies, even though it's public in IL. Public custom formats are constrained to
  `DataFormat<byte[]>`/`DataFormat<string>` (via `CreateBytesApplicationFormat`/
  `CreateStringApplicationFormat`), which can't hold a live `DragInfo` reference (needed for the
  `Source` identity check in `CalculateAutoDragDrop`, comparing against the actual in-flight
  `ITreeDataGridSource`). Fix: `DragInfo.DataFormat` is now a `DataFormat<string>` used only as a
  marker ("a TreeDataGrid row drag is in progress"); the real `DragInfo` travels out-of-band via a
  new `DragInfo.Current` static, set when the drag starts and cleared in a `finally` once
  `DoDragDropAsync` completes. Safe because AutoDragDropRows only supports one drag gesture at a
  time. Builds clean (0 warnings) against Avalonia 11.3.20.

- **Ported to build against Avalonia 12** (package bumped from 11.3.20 to 12.1.1). This keeps our
  own v11-shaped API (class names, `ITreeDataGridSource` as an interface, etc.) exactly as-is - it
  does *not* adopt AvaloniaUI's actual v12 TreeDataGrid API (renamed columns, sources sealed,
  `DragInfo` removed, fluent column API). That product's source was never public even before the
  repo was archived (2025-10-13), so there's nothing to port *to* on that front - this only makes
  our fork compile against the newer Avalonia *core* framework it depends on. Five issues, all
  cheap:
  - `Avalonia.Utilities.MathUtilities` became `internal` (was reachable via `InternalsVisibleTo`
    when TreeDataGrid was first-party). Added `Utils/MathUtilities.cs`: a same-namespace/same-name
    local class covering just the 3 methods actually used (`AreClose`/`GreaterThan`/`IsZero`,
    copied verbatim from Avalonia's public source), which the 8 affected files pick up automatically
    through their existing `using Avalonia.Utilities;` - zero edits needed to those files.
  - `Control.OnLostFocus` parameter type changed `RoutedEventArgs` → `FocusChangedEventArgs`
    (`Primitives/TreeDataGridCell.cs`, `Primitives/TreeDataGridTemplateCell.cs`).
  - `InputElement` gained its own virtual `OnDoubleTapped(TappedEventArgs)`, which
    `TreeDataGridCell.OnDoubleTapped` was hiding rather than overriding (was a CS0114 warning, not
    an error); added `override`.
  - `TopLevel.PlatformSettings` shortcut property removed; replaced with `Application.Current?.
    PlatformSettings` (`Primitives/TreeDataGridCell.cs`, `Selection/TreeDataGridRowSelectionModel.cs`).
  - `DragDrop.DoDragDropAsync` now requires `PointerPressedEventArgs` specifically, not the general
    `PointerEventArgs` it took before. TreeDataGrid's row drag is detected in `OnPointerMoved` (once
    the pointer has traveled past a threshold since the button went down), so by then the original
    press event is gone. Fix: `TreeDataGridRow` now retains the `PointerPressedEventArgs` from
    `OnPointerPressed` in a field and hands that (not the move event) to
    `TreeDataGrid.RaiseRowDragStarted`, whose parameter (and `RunDragDropAsync`'s) widened from
    `PointerEventArgs` to `PointerPressedEventArgs` accordingly.

  Builds clean (0 warnings, 0 errors) against Avalonia 12.1.1. Verified by an actual build, not
  just reading the breaking-changes docs - iterated until every error was gone.
