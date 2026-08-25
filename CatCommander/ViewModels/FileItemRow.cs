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
    /// exclude-from-list decision, because a future multi-select checkbox column will add a
    /// matching IsSelected here - the invariant that must hold once it exists is "selected is
    /// always a subset of visible" (an invisible row must never stay silently selected), and
    /// ApplyFilter is the one place both properties will need to be kept in sync.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    public FileItemRow(IFileSystemItem item, IconCache iconCache)
    {
        Item = item;
        _ = LoadIconAsync(iconCache);
    }

    private async Task LoadIconAsync(IconCache cache) => Icon = await cache.GetIconAsync(Item);
}
