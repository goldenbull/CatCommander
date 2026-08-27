using CatCommander.FileSystem;
using CatCommander.Resources;

namespace CatCommander.Browsing;

public sealed class DirectoryListingSource : IListingSource
{
    private readonly IFileSystemProvider _provider;

    public DirectoryListingSource(
        IFileSystemProvider provider,
        string path,
        Func<ResourceRef, ResourceRef?>? getParent = null)
    {
        _provider = provider;
        Location = new ResourceRef(provider, path);
        Navigation = new DirectoryNavigationPolicy(getParent ?? (location =>
        {
            var parent = provider.GetParentPath(location.Path);
            return string.IsNullOrEmpty(parent) ? null : new ResourceRef(provider, parent);
        }));

        if (provider.ContainerCapabilities != ContainerCapabilities.None)
            WritableDestination = new ContainerRef(Location.Value, provider.ContainerCapabilities);
    }

    public ListingKind Kind => ListingKind.Directory;
    public ResourceRef? Location { get; }
    public ContainerRef? WritableDestination { get; }
    public INavigationPolicy Navigation { get; }

    public async Task<ListingSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var location = Location!.Value;
        var entries = await _provider.ListChildrenAsync(location.Path, ct);
        var items = entries.Select(entry => BrowserItemFactory.Create(_provider, entry, location)).ToList();

        return new ListingSnapshot(items);
    }
}
