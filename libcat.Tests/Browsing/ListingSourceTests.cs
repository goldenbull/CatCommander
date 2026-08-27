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
        Assert.Equal(item.Resource, source.Navigation.GetBackSelection(source, item));
    }

    [Fact]
    public async Task ExpandedListing_IsDepthFirst_DirectoryFirst_AndRetainsDepthAndContainer()
    {
        var branch = Directory.CreateDirectory(Path.Combine(_root, "branch")).FullName;
        var nested = Path.Combine(branch, "nested.txt");
        var rootFile = Path.Combine(_root, "root.txt");
        File.WriteAllText(nested, "nested");
        File.WriteAllText(rootFile, "root");
        var provider = new LocalFileSystemProvider();
        var source = new ExpandedListingSource([new ResourceRef(provider, _root)]);

        var snapshot = await source.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { branch, nested, rootFile }, snapshot.Items.Select(x => x.Resource.Path));
        Assert.Equal(new[] { 0, 1, 0 }, snapshot.Items.Select(x => x.Depth));
        Assert.Equal(new ResourceRef(provider, branch), snapshot.Items[1].Container);
        Assert.Null(source.WritableDestination);
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
