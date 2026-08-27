using CatCommander.Models;
using CatCommander.Resources;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using System.Formats.Tar;
using System.IO.Compression;

namespace CatCommander.FileSystem;

/// <summary>Read-only virtual filesystem over ZIP/7z/RAR/TAR and compression wrappers.</summary>
public sealed class ArchiveFileSystemProvider : IFileSystemProvider, ILocalShellContextProvider, IExternalPathProvider
{
    private readonly string _archivePath;
    private readonly IArchivePasswordStore _passwords;

    public ArchiveFileSystemProvider(string archivePath, IArchivePasswordStore passwords)
    {
        _archivePath = Path.GetFullPath(archivePath);
        _passwords = passwords;
    }

    public string Id => $"archive:{_archivePath}";
    public ResourceCapabilities ResourceCapabilities =>
        ResourceCapabilities.Read | ResourceCapabilities.EnumerateChildren;
    public ContainerCapabilities ContainerCapabilities => ContainerCapabilities.None;
    public bool TracksHistory => false;

    public string? GetParentPath(string path)
    {
        var normalized = NormalizeVirtualPath(path);
        if (normalized == "/") return null;
        var slash = normalized.LastIndexOf('/');
        return slash <= 0 ? "/" : normalized[..slash];
    }

    public ResourceRef? GetParentResource(ResourceRef location) => NormalizeVirtualPath(location.Path) == "/"
        ? new ResourceRef(new LocalFileSystemProvider(), Path.GetDirectoryName(_archivePath)!)
        : new ResourceRef(this, GetParentPath(location.Path)!);

    public ResourceRef GetParentSelectionResource(ResourceRef location) =>
        NormalizeVirtualPath(location.Path) == "/"
            ? new ResourceRef(new LocalFileSystemProvider(), _archivePath)
            : location;

    public Task<IReadOnlyList<IFileSystemItem>> ListChildrenAsync(string path, CancellationToken ct = default) =>
        Task.Run(() => ListChildren(path, ct), ct);

    private IReadOnlyList<IFileSystemItem> ListChildren(string path, CancellationToken ct)
    {
        var parent = NormalizeVirtualPath(path);
        if (IsTarGZip())
            return ListTarGZipChildren(parent, ct);
        if (IsPlainGZip())
            return parent == "/" ? [CreateItem("/" + PlainGZipName(), false, 0, File.GetLastWriteTimeUtc(_archivePath))] : [];
        try
        {
            using var archive = OpenArchive();
            var children = new Dictionary<string, FileItemModel>(StringComparer.Ordinal);
            var encryptedPayloadValidated = false;
            foreach (var entry in archive.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.IsEncrypted && !entry.IsDirectory && !encryptedPayloadValidated)
                {
                    // ZIP commonly exposes filenames without decrypting payloads. Probe one byte
                    // while entering so a missing/wrong password is challenged before an F5 job.
                    using var probe = entry.OpenEntryStream();
                    _ = probe.ReadByte();
                    encryptedPayloadValidated = true;
                }
                if (!TryNormalizeEntry(entry.Key, out var entryPath)) continue;
                if (!TryGetImmediateChild(parent, entryPath, out var childPath, out var impliedDirectory)) continue;

                var isDirectory = impliedDirectory || entry.IsDirectory;
                if (!children.TryGetValue(childPath, out var item) || (!item.CanRead && !isDirectory))
                {
                    children[childPath] = CreateItem(
                        childPath,
                        isDirectory,
                        isDirectory ? 0 : entry.Size,
                        entry.LastModifiedTime ?? DateTime.MinValue);
                }
            }
            return children.Values.OrderByDescending(x => x.ItemType == FileSystemItemType.Directory)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Cast<IFileSystemItem>().ToList();
        }
        catch (Exception ex) when (LooksPasswordRelated(ex))
        {
            throw new ArchivePasswordRequiredException(_archivePath, ex);
        }
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (IsTarGZip())
            return Task.FromResult(OpenTarGZipEntry(path));
        if (IsPlainGZip())
        {
            if (!string.Equals(NormalizeVirtualPath(path), "/" + PlainGZipName(), StringComparison.Ordinal))
                throw new FileNotFoundException("GZip output not found.", path);
            var file = File.OpenRead(_archivePath);
            return Task.FromResult<Stream>(new OwnedReadStream(
                new GZipStream(file, CompressionMode.Decompress), file));
        }
        try
        {
            var archive = OpenArchive();
            var normalized = NormalizeVirtualPath(path).TrimStart('/');
            var entry = archive.Entries.FirstOrDefault(candidate =>
                !candidate.IsDirectory && TryNormalizeEntry(candidate.Key, out var key) &&
                string.Equals(key.TrimStart('/'), normalized, StringComparison.Ordinal));
            if (entry is null)
            {
                archive.Dispose();
                throw new FileNotFoundException("Archive entry not found.", path);
            }
            return Task.FromResult<Stream>(new OwnedReadStream(entry.OpenEntryStream(), archive));
        }
        catch (Exception ex) when (LooksPasswordRelated(ex))
        {
            throw new ArchivePasswordRequiredException(_archivePath, ex);
        }
    }

    public bool CanEnter(IFileSystemItem item) => item.ItemType == FileSystemItemType.Directory;
    public string? GetLocalShellDirectory(ResourceRef location) => Path.GetDirectoryName(_archivePath);
    public string GetExternalPath(string providerPath) => $"{_archivePath}!{NormalizeVirtualPath(providerPath)}";
    public Task<string> CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default) => ReadOnly<string>();
    public Task<string> RenameAsync(string path, string newName, CancellationToken ct = default) => ReadOnly<string>();
    public Task OpenExternallyAsync(string path, CancellationToken ct = default) => ReadOnly();
    public Task CopyAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default) => ReadOnly();
    public Task MoveAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default) => ReadOnly();
    public Task DeleteAsync(string path, CancellationToken ct = default) => ReadOnly();

    private IArchive OpenArchive()
    {
        var options = new ReaderOptions { Password = _passwords.Get(_archivePath) };
        return ArchiveFactory.Open(_archivePath, options);
    }

    private bool IsTarGZip() => _archivePath.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
                                 _archivePath.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);
    private bool IsPlainGZip() => !IsTarGZip() &&
        (_archivePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ||
         _archivePath.EndsWith(".gzip", StringComparison.OrdinalIgnoreCase));
    private string PlainGZipName() => Path.GetFileNameWithoutExtension(_archivePath);

    private IReadOnlyList<IFileSystemItem> ListTarGZipChildren(string parent, CancellationToken ct)
    {
        using var file = File.OpenRead(_archivePath);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        var children = new Dictionary<string, FileItemModel>(StringComparer.Ordinal);
        while (tar.GetNextEntry(copyData: false) is { } entry)
        {
            ct.ThrowIfCancellationRequested();
            if (!TryNormalizeEntry(entry.Name, out var entryPath) ||
                !TryGetImmediateChild(parent, entryPath, out var childPath, out var impliedDirectory)) continue;
            var isDirectory = impliedDirectory || entry.EntryType == TarEntryType.Directory;
            children.TryAdd(childPath, CreateItem(childPath, isDirectory, isDirectory ? 0 : entry.Length,
                entry.ModificationTime.UtcDateTime));
        }
        return children.Values.OrderByDescending(x => x.ItemType == FileSystemItemType.Directory)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).Cast<IFileSystemItem>().ToList();
    }

    private Stream OpenTarGZipEntry(string path)
    {
        var file = File.OpenRead(_archivePath);
        var gzip = new GZipStream(file, CompressionMode.Decompress);
        var tar = new TarReader(gzip);
        var normalized = NormalizeVirtualPath(path);
        try
        {
            while (tar.GetNextEntry(copyData: false) is { } entry)
            {
                if (TryNormalizeEntry(entry.Name, out var entryPath) && entry.EntryType != TarEntryType.Directory &&
                    string.Equals(entryPath, normalized, StringComparison.Ordinal))
                {
                    return new OwnedReadStream(entry.DataStream!, new CompositeOwner(tar, gzip, file));
                }
            }
            throw new FileNotFoundException("Tar entry not found.", path);
        }
        catch { tar.Dispose(); gzip.Dispose(); file.Dispose(); throw; }
    }

    private static FileItemModel CreateItem(string path, bool directory, long size, DateTime modified) => new()
    {
        Name = path[(path.LastIndexOf('/') + 1)..], FullPath = path,
        Extension = directory ? string.Empty : Path.GetExtension(path),
        Size = size, Modified = modified, ItemType = directory ? FileSystemItemType.Directory : FileSystemItemType.File,
        CanRead = true, CanWrite = false,
    };

    private static bool TryNormalizeEntry(string? key, out string path)
    {
        path = "/";
        if (string.IsNullOrWhiteSpace(key)) return false;
        var parts = key.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Any(part => part is "." or "..")) return false;
        path = "/" + string.Join('/', parts);
        return parts.Length > 0;
    }

    private static string NormalizeVirtualPath(string path) =>
        path == "/" ? "/" : "/" + path.Replace('\\', '/').Trim('/');

    private static bool TryGetImmediateChild(string parent, string entry, out string child, out bool directory)
    {
        child = string.Empty; directory = false;
        var prefix = parent == "/" ? "/" : parent + "/";
        if (!entry.StartsWith(prefix, StringComparison.Ordinal) || entry == parent) return false;
        var remainder = entry[prefix.Length..];
        var slash = remainder.IndexOf('/');
        directory = slash >= 0;
        child = prefix + (directory ? remainder[..slash] : remainder);
        return true;
    }

    private static bool LooksPasswordRelated(Exception ex) =>
        ex.Message.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
        (ex.InnerException is not null && LooksPasswordRelated(ex.InnerException));

    private static Task ReadOnly() => Task.FromException(new NotSupportedException("Archive providers are read-only."));
    private static Task<T> ReadOnly<T>() => Task.FromException<T>(new NotSupportedException("Archive providers are read-only."));

    private sealed class OwnedReadStream(Stream inner, IDisposable owner) : Stream
    {
        public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => false;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush(); public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin); public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) => await inner.ReadAsync(buffer, ct);
        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); owner.Dispose(); } base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); owner.Dispose(); GC.SuppressFinalize(this); }
    }

    private sealed class CompositeOwner(params IDisposable[] owners) : IDisposable
    {
        public void Dispose() { foreach (var owner in owners) owner.Dispose(); }
    }
}
