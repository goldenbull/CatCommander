using CatCommander.Resources;

namespace CatCommander.Browsing;

public sealed class SearchResultListingSource(
    Func<CancellationToken, Task<IReadOnlyList<BrowserItem>>> search) : IListingSource
{
    public ListingKind Kind => ListingKind.SearchResults;
    public ResourceRef? Location => null;
    public ContainerRef? WritableDestination => null;
    public INavigationPolicy Navigation => ProjectedListingNavigationPolicy.Instance;

    public async Task<ListingSnapshot> LoadAsync(CancellationToken ct = default) =>
        new(await search(ct));
}

/// <summary>
/// Total Commander-style branch view. It recursively projects descendants from one or more real
/// provider containers into one list; every result retains its own provider and immediate
/// container, while the projection itself is deliberately not a writable destination.
/// </summary>
public sealed class ExpandedListingSource(IReadOnlyList<ResourceRef> roots) : IListingSource
{
    public ListingKind Kind => ListingKind.ExpandedResults;
    public ResourceRef? Location => null;
    public ContainerRef? WritableDestination => null;
    public INavigationPolicy Navigation => ProjectedListingNavigationPolicy.Instance;

    public async Task<ListingSnapshot> LoadAsync(CancellationToken ct = default)
    {
        var results = new List<BrowserItem>();
        var pending = new Queue<(ResourceRef Container, int Depth)>();
        var visited = new HashSet<ResourceRef>();

        foreach (var root in roots)
            pending.Enqueue((root, 0));

        while (pending.TryDequeue(out var next))
        {
            ct.ThrowIfCancellationRequested();
            if (!visited.Add(next.Container))
                continue;

            var entries = await next.Container.Provider.ListChildrenAsync(next.Container.Path, ct);
            foreach (var entry in entries)
            {
                var item = BrowserItemFactory.Create(
                    next.Container.Provider,
                    entry,
                    next.Container,
                    next.Depth);
                results.Add(item);

                if (item.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren))
                    pending.Enqueue((item.Resource, next.Depth + 1));
            }
        }

        return new ListingSnapshot(results);
    }
}
