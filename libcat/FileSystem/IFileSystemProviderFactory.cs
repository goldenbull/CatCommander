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

    /// <summary>Whether a local-looking item is actually an enterable provider root.</summary>
    bool CanEnter(string path) => false;

    /// <summary>Initial provider-local path when entering a recognized root.</summary>
    string GetInitialPath(string path) => path;

    IFileSystemProvider Create(string path);
}
