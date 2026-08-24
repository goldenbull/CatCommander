namespace CatCommander.FileSystem;

/// <summary>
/// Recognizes paths belonging to one kind of source and constructs the matching provider.
/// FileSystemProviderRegistry tries registered factories in order; register more specific
/// factories (archive, SFTP) before LocalFileSystemProviderFactory, which matches unconditionally
/// as the catch-all.
/// </summary>
public interface IFileSystemProviderFactory
{
    bool CanHandle(string path);

    IFileSystemProvider Create(string path);
}
