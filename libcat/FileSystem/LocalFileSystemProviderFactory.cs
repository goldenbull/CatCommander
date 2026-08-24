namespace CatCommander.FileSystem;

/// <summary>
/// Matches unconditionally - register this one last in FileSystemProviderRegistry so more
/// specific factories (archive, SFTP, once they exist) get first refusal.
/// </summary>
public class LocalFileSystemProviderFactory : IFileSystemProviderFactory
{
    public bool CanHandle(string path) => true;

    public IFileSystemProvider Create(string path) => new LocalFileSystemProvider();
}
