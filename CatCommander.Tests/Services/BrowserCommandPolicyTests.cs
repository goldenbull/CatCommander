using CatCommander.Browsing;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;
using CatCommander.Services;

namespace CatCommander.Tests.Services;

public sealed class BrowserCommandPolicyTests
{
    private readonly LocalFileSystemProvider _provider = new();
    private readonly BrowserCommandPolicy _policy = new();

    [Fact]
    public void ReadOnlyArchiveFile_CanCopyButCannotMoveOrDelete()
    {
        var source = File("archive.zip!/a.txt", ResourceCapabilities.Read);
        var destination = Destination(ContainerCapabilities.AcceptFiles);

        Assert.True(_policy.CanCopy([source], destination));
        Assert.False(_policy.CanMove([source], destination));
        Assert.False(_policy.CanDelete([source]));
    }

    [Fact]
    public void ExpandedResults_CannotBeUsedAsDestination()
    {
        var source = File("/source/a.txt", ResourceCapabilities.Read | ResourceCapabilities.Delete);

        Assert.False(_policy.CanCopy([source], destination: null));
        Assert.False(_policy.CanMove([source], destination: null));
        Assert.False(_policy.CanCreateDirectory(context: null));
    }

    [Fact]
    public void DirectoryRequiresEnumerationAndDirectoryDestinationCapability()
    {
        var directory = Directory("archive.zip!/folder", ResourceCapabilities.EnumerateChildren);

        Assert.False(_policy.CanCopy([directory], Destination(ContainerCapabilities.AcceptFiles)));
        Assert.True(_policy.CanCopy([directory], Destination(ContainerCapabilities.AcceptDirectories)));
    }

    [Fact]
    public void MixedSelectionUsesLeastCapableItem()
    {
        var writable = File("/source/a.txt", ResourceCapabilities.Read | ResourceCapabilities.Delete);
        var readOnly = File("archive.zip!/b.txt", ResourceCapabilities.Read);
        var destination = Destination(ContainerCapabilities.AcceptFiles);

        Assert.True(_policy.CanCopy([writable, readOnly], destination));
        Assert.False(_policy.CanMove([writable, readOnly], destination));
        Assert.False(_policy.CanDelete([writable, readOnly]));
    }

    private BrowserItem File(string path, ResourceCapabilities capabilities) =>
        Item(path, FileSystemItemType.File, capabilities);

    private BrowserItem Directory(string path, ResourceCapabilities capabilities) =>
        Item(path, FileSystemItemType.Directory, capabilities);

    private BrowserItem Item(
        string path,
        FileSystemItemType itemType,
        ResourceCapabilities capabilities) => new(
        new FileItemModel
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            ItemType = itemType,
            CanRead = capabilities.HasFlag(ResourceCapabilities.Read),
            CanWrite = capabilities.HasFlag(ResourceCapabilities.Delete),
        },
        new ResourceRef(_provider, path),
        null,
        capabilities);

    private ContainerRef Destination(ContainerCapabilities capabilities) =>
        new(new ResourceRef(_provider, "/destination"), capabilities);
}
