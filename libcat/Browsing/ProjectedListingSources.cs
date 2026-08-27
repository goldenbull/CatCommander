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
        var visited = new HashSet<ResourceRef>();
        var pending = new Stack<(ResourceRef? Container, BrowserItem? Item, int Depth)>();

        for (var i = roots.Count - 1; i >= 0; i--)
            pending.Push((roots[i], null, 0));

        while (pending.TryPop(out var next))
        {
            ct.ThrowIfCancellationRequested();
            if (next.Item is { } item)
            {
                results.Add(item);
                if (item.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren))
                    pending.Push((item.Resource, null, next.Depth + 1));
                continue;
            }

            var container = next.Container!.Value;
            if (!visited.Add(container))
                continue;

            var entries = (await container.Provider.ListChildrenAsync(container.Path, ct))
                .OrderByDescending(entry => entry.ItemType == Models.FileSystemItemType.Directory)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            for (var i = entries.Count - 1; i >= 0; i--)
            {
                pending.Push((null, BrowserItemFactory.Create(
                    container.Provider, entries[i], container, next.Depth), next.Depth));
            }
        }

        return new ListingSnapshot(results);
    }
}
