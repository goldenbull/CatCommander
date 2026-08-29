namespace CatCommander.FileSystem;

public sealed class ArchiveFileSystemProviderFactory : IFileSystemProviderFactory
{
    private static readonly string[] Extensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".gzip", ".tgz", ".bz2", ".xz", ".iso"];
    private readonly IProviderCredentialStore _credentials;
    private readonly IFileSystemProvider _backingProvider;

    public ArchiveFileSystemProviderFactory(
        IProviderCredentialStore credentials,
        IFileSystemProvider backingProvider)
    {
        _credentials = credentials;
        _backingProvider = backingProvider;
    }

    public bool CanHandle(string path) => TrySplit(path, out _, out _);
    public bool CanEnter(string path) => TrySplit(path, out _, out _);
    public string GetInitialPath(string path) => TrySplit(path, out _, out var inner) ? inner : "/";
    public IFileSystemProvider Create(string path)
    {
        if (!TrySplit(path, out var archive, out _)) throw new NotSupportedException(path);
        var archiveResource = new Resources.ResourceRef(_backingProvider, archive);
        var containerResource = new Resources.ResourceRef(
            _backingProvider,
            Path.GetDirectoryName(archive) ?? Path.GetPathRoot(archive) ?? archive);
        return archive.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
            ? new IsoFileSystemProvider(archive, archiveResource, containerResource)
            : new ArchiveFileSystemProvider(archive, _credentials, archiveResource, containerResource);
    }

    private static bool TrySplit(string path, out string archivePath, out string innerPath)
    {
        var marker = path.IndexOf("!/", StringComparison.Ordinal);
        archivePath = marker >= 0 ? path[..marker] : path;
        innerPath = marker >= 0 ? "/" + path[(marker + 2)..].Trim('/') : "/";
        var candidate = archivePath;
        return File.Exists(archivePath) && Extensions.Any(extension =>
            candidate.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
    }
}
