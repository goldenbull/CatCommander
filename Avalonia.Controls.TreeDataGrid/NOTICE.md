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

(none yet)
