using System.Diagnostics;
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
                        IsHidden = IsHiddenEntry(info.Name, info.Attributes),
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
                        IsHidden = IsHiddenEntry(info.Name, info.Attributes),
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

    // Cmd/Ctrl+. (ItemBrowserViewModel.ToggleHiddenFiles) needs "is this hidden" to mean the same
    // thing on every platform without the caller branching on OS: a leading '.' (the only signal
    // macOS/Linux ever have) OR the Windows Hidden file attribute (which .NET may or may not also
    // set for a dot-file on Unix, depending on runtime/filesystem - the OR makes that irrelevant
    // either way, rather than depending on FileAttributes.Hidden alone to already reflect it).
    private static bool IsHiddenEntry(string name, FileAttributes attributes) =>
        name.StartsWith('.') || attributes.HasFlag(FileAttributes.Hidden);

    public Task<string> CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var fullPath = Path.Combine(parentPath, name);
            Directory.CreateDirectory(fullPath);
            return fullPath;
        }, ct);
    }

    public Task<string> RenameAsync(string path, string newName, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent))
                throw new InvalidOperationException($"'{path}' has no parent directory to rename within.");

            var newPath = Path.Combine(parent, newName);

            if (Directory.Exists(path))
                Directory.Move(path, newPath);
            else
                File.Move(path, newPath);

            return newPath;
        }, ct);
    }

    // UseShellExecute=true is what makes this launch the OS's own default association (Finder/
    // Explorer double-click behavior) instead of trying to execute the file as a program, which is
    // what a plain Process.Start(path) does on .NET's non-Windows platforms.
    public Task OpenExternallyAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }), ct);

    public Task CopyAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default) =>
        Task.Run(() => CopyRecursive(sourcePath, destinationDirectory, progress, ct), ct);

    public Task MoveAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));
            var destPath = Path.Combine(destinationDirectory, name);

            try
            {
                if (Directory.Exists(sourcePath))
                    Directory.Move(sourcePath, destPath);
                else
                    File.Move(sourcePath, destPath, overwrite: true);

                progress?.Report(destPath);
            }
            catch (IOException)
            {
                // Directory.Move/File.Move are only an atomic rename within the same volume, and
                // Directory.Move also refuses to merge into an already-existing destination
                // directory - either failure falls back to a real copy-then-delete instead of
                // propagating, so a cross-volume Move still succeeds, just not atomically.
                CopyRecursive(sourcePath, destinationDirectory, progress, ct);
                DeleteRecursive(sourcePath);
            }
        }, ct);
    }

    private static void CopyRecursive(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var name = Path.GetFileName(sourcePath.TrimEnd(Path.DirectorySeparatorChar));
        var destPath = Path.Combine(destinationDirectory, name);

        if (Directory.Exists(sourcePath))
        {
            Directory.CreateDirectory(destPath);
            foreach (var dir in Directory.EnumerateDirectories(sourcePath))
                CopyRecursive(dir, destPath, progress, ct);
            foreach (var file in Directory.EnumerateFiles(sourcePath))
                CopyRecursive(file, destPath, progress, ct);
        }
        else
        {
            File.Copy(sourcePath, destPath, overwrite: true);
            progress?.Report(destPath);
        }
    }

    private static void DeleteRecursive(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
        else
            File.Delete(path);
    }

    public Task DeleteAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => DeleteRecursive(path), ct);

    public bool CanEnter(IFileSystemItem item) => item.ItemType is FileSystemItemType.Directory;

    public bool TracksHistory => true;
}
