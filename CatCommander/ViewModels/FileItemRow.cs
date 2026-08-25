using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CatCommander.Models;
using CatCommander.Services;
using Metalama.Patterns.Observability;

namespace CatCommander.ViewModels;

/// <summary>
/// Wraps an IFileSystemItem as a TreeDataGrid row model, adding an asynchronously-loaded Icon.
/// IFileSystemItem itself stays Avalonia-free (libcat); this wrapper is where the UI-facing Bitmap
/// appears, one layer up.
/// </summary>
[Observable]
public partial class FileItemRow
{
    public IFileSystemItem Item { get; }

    /// <summary>
    /// Null until the async icon lookup completes, then triggers a property-changed notification
    /// so the cell re-renders. No cancellation in this round - see IconCache's doc comment on why
    /// the number of in-flight loads per folder view is bounded (by distinct extension count),
    /// not proportional to file count.
    /// </summary>
    public Bitmap? Icon { get; private set; }

    /// <summary>
    /// Whether this row currently passes ItemBrowserViewModel's quick filter (see
    /// ItemBrowserViewModel.ApplyFilter) - false rows are excluded from Source.Items so
    /// TreeDataGrid never renders them. Kept as a real per-row property, not just an
    /// exclude-from-list decision, because IsMarked below must hold the invariant "marked is
    /// always a subset of visible" (an invisible row must never stay silently marked) - ApplyFilter
    /// is the one place both properties are kept in sync.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// The checkbox-style "marked" state Total Commander calls multi-selection - Space toggles it
    /// (ItemBrowserViewModel.ToggleMarkCurrentItem), deliberately separate from the grid's own
    /// single-cursor SelectionModel (which just tracks "what row is highlighted right now" and
    /// moves on arrow keys/click, unaffected by marking). Copy/Move/Delete act on every marked row
    /// (ItemBrowserViewModel.GetOperationTargets), falling back to the cursor row when nothing is
    /// marked. Reset to false whenever _rows is rebuilt (RebuildSource - every navigation and
    /// view-mode toggle), since a fresh FileItemRow always starts unmarked - per-tab persistence
    /// and "don't remember marks across directories" both fall out of that for free.
    /// </summary>
    public bool IsMarked { get; set; }

    public FileItemRow(IFileSystemItem item, IconCache iconCache)
    {
        Item = item;
        _ = LoadIconAsync(iconCache);
    }

    private async Task LoadIconAsync(IconCache cache) => Icon = await cache.GetIconAsync(Item);
}
