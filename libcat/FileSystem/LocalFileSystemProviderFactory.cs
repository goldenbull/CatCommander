namespace CatCommander.FileSystem;

/// <summary>
/// Matches unconditionally - register this one last in FileSystemProviderRegistry so more
/// specific factories (archive, SFTP, once they exist) get first refusal.
/// </summary>
public class LocalFileSystemProviderFactory : IFileSystemProviderFactory
{
    private readonly IFileSystemProvider _provider;

    public LocalFileSystemProviderFactory(IFileSystemProvider? provider = null) =>
        _provider = provider ?? new LocalFileSystemProvider();

    public bool CanHandle(string path) => true;

    public IFileSystemProvider Create(string path) => _provider;
}
