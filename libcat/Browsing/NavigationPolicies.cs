using CatCommander.Resources;

namespace CatCommander.Browsing;

public sealed class DirectoryNavigationPolicy(Func<ResourceRef, ResourceRef?> getParent) : INavigationPolicy
{
    public ResourceRef? GetBackTarget(IListingSource source, BrowserItem? currentItem) =>
        source.Location is { } location ? getParent(location) : null;
}

public sealed class ProjectedListingNavigationPolicy : INavigationPolicy
{
    public static ProjectedListingNavigationPolicy Instance { get; } = new();

    private ProjectedListingNavigationPolicy()
    {
    }

    public ResourceRef? GetBackTarget(IListingSource source, BrowserItem? currentItem) =>
        currentItem?.Container;
}
