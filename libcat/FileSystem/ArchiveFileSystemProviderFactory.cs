namespace CatCommander.FileSystem;

public sealed class ArchiveFileSystemProviderFactory : IFileSystemProviderFactory
{
    private static readonly string[] Extensions =
        [".zip", ".7z", ".rar", ".tar", ".gz", ".gzip", ".tgz", ".bz2", ".xz", ".iso"];
    private readonly IArchivePasswordStore _passwords;

    public ArchiveFileSystemProviderFactory(IArchivePasswordStore passwords) => _passwords = passwords;

    public bool CanHandle(string path) => TrySplit(path, out _, out _);
    public bool CanEnter(string path) => TrySplit(path, out _, out _);
    public string GetInitialPath(string path) => TrySplit(path, out _, out var inner) ? inner : "/";
    public IFileSystemProvider Create(string path)
    {
        if (!TrySplit(path, out var archive, out _)) throw new NotSupportedException(path);
        return archive.EndsWith(".iso", StringComparison.OrdinalIgnoreCase)
            ? new IsoFileSystemProvider(archive)
            : new ArchiveFileSystemProvider(archive, _passwords);
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
