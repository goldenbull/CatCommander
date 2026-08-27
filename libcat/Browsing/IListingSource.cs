using CatCommander.Resources;

namespace CatCommander.Browsing;

public enum ListingKind
{
    Directory,
    SearchResults,
    ExpandedResults,
}

public sealed record ListingSnapshot(IReadOnlyList<BrowserItem> Items);

/// <summary>
/// Describes what a browser tab is showing. Providers own resources; listing sources only project
/// those resources into a grid and define the meaning of navigation in that projection.
/// </summary>
public interface IListingSource
{
    ListingKind Kind { get; }
    ResourceRef? Location { get; }
    ContainerRef? WritableDestination { get; }
    INavigationPolicy Navigation { get; }

    Task<ListingSnapshot> LoadAsync(CancellationToken ct = default);
}

public interface INavigationPolicy
{
    ResourceRef? GetBackTarget(IListingSource source, BrowserItem? currentItem);
    ResourceRef? GetBackSelection(IListingSource source, BrowserItem? currentItem);
}
