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

    public FileItemRow(IFileSystemItem item, IconCache iconCache)
    {
        Item = item;
        _ = LoadIconAsync(iconCache);
    }

    private async Task LoadIconAsync(IconCache cache) => Icon = await cache.GetIconAsync(Item);
}
