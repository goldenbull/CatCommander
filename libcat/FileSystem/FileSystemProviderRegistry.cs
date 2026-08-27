using CatCommander.Resources;

namespace CatCommander.FileSystem;

/// <summary>
/// Resolves a path string to the IFileSystemProvider that should handle it. This is the one place
/// that will need new registrations when archive/SFTP providers are added - the ViewModel/View
/// layer only ever talks to IFileSystemProvider, never a concrete provider type.
/// </summary>
public class FileSystemProviderRegistry
{
    private readonly List<IFileSystemProviderFactory> _factories = new();

    public void Register(IFileSystemProviderFactory factory) => _factories.Add(factory);

    /// <summary>
    /// Resolves <paramref name="path"/> to a provider and the path to pass into that provider's
    /// own ListChildrenAsync/OpenReadAsync. For LocalFileSystemProvider, relativePath is just
    /// <paramref name="path"/> unchanged - the split only becomes meaningful once a factory can
    /// recognize "part of this path is inside an archive".
    /// </summary>
    public Task<(IFileSystemProvider Provider, string RelativePath)> ResolveAsync(string path)
    {
        foreach (var factory in _factories)
        {
            if (factory.CanHandle(path))
                return Task.FromResult((factory.Create(path), factory.GetInitialPath(path)));
        }

        throw new InvalidOperationException($"No registered IFileSystemProviderFactory can handle '{path}'.");
    }

    public bool TryResolveEnterable(string path, out ResourceRef resource)
    {
        foreach (var factory in _factories)
        {
            if (factory.CanEnter(path))
            {
                resource = new ResourceRef(factory.Create(path), factory.GetInitialPath(path));
                return true;
            }
        }

        resource = default;
        return false;
    }
}
