using CatCommander.Browsing;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;

namespace CatCommander.Tests.Browsing;

public sealed class ListingSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"CatCommanderListingTests_{Guid.NewGuid():N}");

    public ListingSourceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task DirectoryListing_PreservesProviderAndContainerForEveryItem()
    {
        var child = Directory.CreateDirectory(Path.Combine(_root, "child"));
        var provider = new LocalFileSystemProvider();
        var location = new ResourceRef(provider, _root);
        var source = new DirectoryListingSource(provider, _root, _ => null);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken);

        var item = Assert.Single(snapshot.Items);
        Assert.Same(provider, item.Resource.Provider);
        Assert.Equal(child.FullName, item.Resource.Path);
        Assert.Equal(location, item.Container);
        Assert.True(item.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren));
        Assert.NotNull(source.WritableDestination);
    }

    [Fact]
    public void ProjectedNavigation_ReturnsSelectedItemsOwnContainer()
    {
        var provider = new LocalFileSystemProvider();
        var container = new ResourceRef(provider, _root);
        var item = new BrowserItem(
            new FileItemModel { Name = "a.txt", FullPath = Path.Combine(_root, "a.txt") },
            new ResourceRef(provider, Path.Combine(_root, "a.txt")),
            container,
            ResourceCapabilities.Read);
        var source = new StubProjectedListing();

        var target = source.Navigation.GetBackTarget(source, item);

        Assert.Equal(container, target);
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private sealed class StubProjectedListing : IListingSource
    {
        public ListingKind Kind => ListingKind.SearchResults;
        public ResourceRef? Location => null;
        public ContainerRef? WritableDestination => null;
        public INavigationPolicy Navigation => ProjectedListingNavigationPolicy.Instance;
        public Task<ListingSnapshot> LoadAsync(CancellationToken ct = default) =>
            Task.FromResult(new ListingSnapshot([]));
    }
}
