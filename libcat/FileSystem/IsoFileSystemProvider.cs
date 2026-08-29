using CatCommander.Models;
using CatCommander.Resources;
using DiscUtils.Iso9660;

namespace CatCommander.FileSystem;

public sealed class IsoFileSystemProvider : IFileSystemProvider, ILocalShellContextProvider, IExternalPathProvider
{
    private readonly string _isoPath;
    private readonly ResourceRef _backingArchive;
    private readonly ResourceRef _backingContainer;

    public IsoFileSystemProvider(string isoPath, ResourceRef backingArchive, ResourceRef backingContainer)
    {
        _isoPath = Path.GetFullPath(isoPath);
        _backingArchive = backingArchive;
        _backingContainer = backingContainer;
    }
    public string Id => $"iso:{_isoPath}";
    public ResourceCapabilities ResourceCapabilities => ResourceCapabilities.Read | ResourceCapabilities.EnumerateChildren;
    public ContainerCapabilities ContainerCapabilities => ContainerCapabilities.None;
    public bool TracksHistory => false;
    public bool SupportsTreeMode => true;

    public string? GetParentPath(string path)
    {
        var normalized = Normalize(path);
        if (normalized == "/") return null;
        var slash = normalized.LastIndexOf('/');
        return slash <= 0 ? "/" : normalized[..slash];
    }

    public ResourceRef? GetParentResource(ResourceRef location) => Normalize(location.Path) == "/"
        ? _backingContainer
        : new ResourceRef(this, GetParentPath(location.Path)!);

    public ResourceRef GetParentSelectionResource(ResourceRef location) => Normalize(location.Path) == "/"
        ? _backingArchive
        : location;

    public Task<IReadOnlyList<IFileSystemItem>> ListChildrenAsync(string path, CancellationToken ct = default) => Task.Run(() =>
    {
        using var stream = File.OpenRead(_isoPath);
        using var reader = new CDReader(stream, true, true);
        var discPath = ToDiscPath(path);
        var result = new List<IFileSystemItem>();
        foreach (var directory in reader.GetDirectories(discPath))
        {
            ct.ThrowIfCancellationRequested();
            result.Add(Item(directory, true, 0, reader.GetDirectoryInfo(directory).LastWriteTimeUtc));
        }
        foreach (var file in reader.GetFiles(discPath))
        {
            ct.ThrowIfCancellationRequested();
            var info = reader.GetFileInfo(file);
            result.Add(Item(file, false, info.Length, info.LastWriteTimeUtc));
        }
        return (IReadOnlyList<IFileSystemItem>)result;
    }, ct);

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var source = File.OpenRead(_isoPath);
        var reader = new CDReader(source, true, true);
        try
        {
            return Task.FromResult<Stream>(new OwnedIsoStream(
                reader.OpenFile(ToDiscPath(path), FileMode.Open, FileAccess.Read), reader, source));
        }
        catch { reader.Dispose(); source.Dispose(); throw; }
    }

    public bool CanEnter(IFileSystemItem item) => item.ItemType == FileSystemItemType.Directory;
    public string? GetLocalShellDirectory(ResourceRef location) => Path.GetDirectoryName(_isoPath);
    public string GetExternalPath(string providerPath) => $"{_isoPath}!{Normalize(providerPath)}";
    private static FileItemModel Item(string discPath, bool directory, long size, DateTime modified)
    {
        var path = Normalize(discPath);
        return new FileItemModel { Name = path[(path.LastIndexOf('/') + 1)..], FullPath = path,
            Extension = directory ? string.Empty : FileNameUtility.GetExtension(Path.GetFileName(path)), Size = size, Modified = modified,
            ItemType = directory ? FileSystemItemType.Directory : FileSystemItemType.File, CanRead = true, CanWrite = false };
    }
    private static string Normalize(string path) => path == "/" ? "/" : "/" + path.Replace('\\', '/').Trim('/');
    private static string ToDiscPath(string path) => Normalize(path) == "/" ? "\\" : Normalize(path).Replace('/', '\\');
    private sealed class OwnedIsoStream(Stream inner, IDisposable reader, IDisposable source) : Stream
    {
        public override bool CanRead => inner.CanRead; public override bool CanSeek => inner.CanSeek; public override bool CanWrite => false;
        public override long Length => inner.Length; public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush(); public override int Read(byte[] b, int o, int c) => inner.Read(b, o, c);
        public override long Seek(long o, SeekOrigin so) => inner.Seek(o, so); public override void SetLength(long v) => throw new NotSupportedException();
        public override void Write(byte[] b, int o, int c) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> b, CancellationToken ct = default) => inner.ReadAsync(b, ct);
        protected override void Dispose(bool disposing) { if (disposing) { inner.Dispose(); reader.Dispose(); source.Dispose(); } base.Dispose(disposing); }
    }
}
