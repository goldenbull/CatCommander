using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CatCommander.Models;
using CatCommander.Utils;

namespace CatCommander.Services;

/// <summary>
/// Caches file/folder icons by extension (not by full path) - this is where Avalonia.Bitmap first
/// appears; SystemIconProvider (libcat) only ever hands back byte[]. Caching by extension is safe
/// because the underlying OS APIs already resolve ordinary files' icons by extension/UTI, not by
/// reading a specific file's embedded resources - see SystemIconProvider's doc comment. This means
/// the number of native icon lookups triggered by browsing a folder is bounded by how many
/// distinct extensions appear in it, not by file count.
/// </summary>
public class IconCache
{
    private readonly ConcurrentDictionary<string, Task<Bitmap?>> _cache = new();

    public Task<Bitmap?> GetIconAsync(IFileSystemItem item)
    {
        var key = item.ItemType == FileSystemItemType.Directory
            ? "folder"
            : string.IsNullOrEmpty(item.Extension) ? "file" : item.Extension.ToLowerInvariant();

        return _cache.GetOrAdd(key, _ => FetchAsync(item));
    }

    private static async Task<Bitmap?> FetchAsync(IFileSystemItem item)
    {
        var isDirectory = item.ItemType == FileSystemItemType.Directory;
        var bytes = await Task.Run(() => SystemIconProvider.GetIconBytes(item.FullPath, isDirectory));
        return bytes is null ? null : new Bitmap(new MemoryStream(bytes));
    }
}
