using CatCommander.Models;

namespace CatCommander.FileSystem;

/// <summary>
/// Browses the real local file system. Wraps synchronous System.IO calls in Task.Run so the
/// interface stays consistent with providers that have genuine network latency (SFTP, etc.) -
/// callers on the UI thread never block regardless of which provider they're talking to.
/// </summary>
public class LocalFileSystemProvider : IFileSystemProvider
{
    public Task<IReadOnlyList<IFileSystemItem>> ListChildrenAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(IReadOnlyList<IFileSystemItem> () =>
        {
            var items = new List<IFileSystemItem>();

            foreach (var dir in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new DirectoryInfo(dir);
                    items.Add(new FileItemModel
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        ItemType = FileSystemItemType.Directory,
                        Created = info.CreationTime,
                        Modified = info.LastWriteTime,
                        Accessed = info.LastAccessTime,
                        IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden),
                    });
                }
                catch (Exception)
                {
                    // Inaccessible entries (permissions, races with concurrent deletion, ...) are
                    // skipped rather than failing the whole listing.
                }
            }

            foreach (var file in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    items.Add(new FileItemModel
                    {
                        Name = info.Name,
                        FullPath = info.FullName,
                        Extension = info.Extension,
                        Size = info.Length,
                        ItemType = FileSystemItemType.File,
                        Created = info.CreationTime,
                        Modified = info.LastWriteTime,
                        Accessed = info.LastAccessTime,
                        IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden),
                        CanWrite = !info.IsReadOnly,
                    });
                }
                catch (Exception)
                {
                }
            }

            return items;
        }, ct);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        return Task.Run(Stream () => File.OpenRead(path), ct);
    }

    public bool CanEnter(IFileSystemItem item) => item.ItemType is FileSystemItemType.Directory;

    public bool TracksHistory => true;
}
