using CatCommander.Browsing;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;

namespace CatCommander.Tests.FileSystem;

public sealed class ResourceTransferServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"CatCommanderTransferTests_{Guid.NewGuid():N}");

    public ResourceTransferServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task CopyAsync_CopiesBetweenDifferentProviderInstances()
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source")).FullName;
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var sourcePath = Path.Combine(sourceDirectory, "a.txt");
        await File.WriteAllTextAsync(sourcePath, "hello", TestContext.Current.CancellationToken);

        var sourceProvider = new LocalFileSystemProvider();
        var destinationProvider = new LocalFileSystemProvider();
        var source = new BrowserItem(
            new FileItemModel
            {
                Name = "a.txt",
                FullPath = sourcePath,
                ItemType = FileSystemItemType.File,
                CanRead = true,
                CanWrite = true,
            },
            new ResourceRef(sourceProvider, sourcePath),
            new ResourceRef(sourceProvider, sourceDirectory),
            ResourceCapabilities.Read | ResourceCapabilities.Delete);
        var destination = new ContainerRef(
            new ResourceRef(destinationProvider, destinationDirectory),
            ((IFileSystemProvider)destinationProvider).ContainerCapabilities);

        await new ResourceTransferService().CopyAsync(
            source,
            destination,
            ct: TestContext.Current.CancellationToken);

        Assert.Equal("hello", await File.ReadAllTextAsync(
            Path.Combine(destinationDirectory, "a.txt"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveAsync_BetweenInstancesOfSameFileSystemUsesNativeMoveWithoutTrash()
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "move-source")).FullName;
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "move-destination")).FullName;
        var sourcePath = Path.Combine(sourceDirectory, "move.txt");
        await File.WriteAllTextAsync(sourcePath, "move me", TestContext.Current.CancellationToken);
        var trash = new RecordingTrashService();
        var sourceProvider = new LocalFileSystemProvider(trash);
        var destinationProvider = new LocalFileSystemProvider();
        var source = CreateFile(sourceProvider, sourcePath, ResourceCapabilities.Read | ResourceCapabilities.Delete);
        var destination = new ContainerRef(
            new ResourceRef(destinationProvider, destinationDirectory),
            ContainerCapabilities.AcceptFiles);

        await new ResourceTransferService().MoveAsync(
            source,
            destination,
            ct: TestContext.Current.CancellationToken);

        Assert.False(File.Exists(sourcePath));
        Assert.Empty(trash.Paths);
        Assert.Equal("move me", await File.ReadAllTextAsync(
            Path.Combine(destinationDirectory, "move.txt"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MoveAsync_ReadOnlySourceFailsBeforeCopying()
    {
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "readonly-source")).FullName;
        var destinationDirectory = Directory.CreateDirectory(Path.Combine(_root, "readonly-destination")).FullName;
        var sourcePath = Path.Combine(sourceDirectory, "readonly.txt");
        await File.WriteAllTextAsync(sourcePath, "keep me", TestContext.Current.CancellationToken);
        var sourceProvider = new LocalFileSystemProvider();
        var destinationProvider = new LocalFileSystemProvider();
        var source = CreateFile(sourceProvider, sourcePath, ResourceCapabilities.Read);
        var destination = new ContainerRef(
            new ResourceRef(destinationProvider, destinationDirectory),
            ContainerCapabilities.AcceptFiles);

        await Assert.ThrowsAsync<NotSupportedException>(() => new ResourceTransferService().MoveAsync(
            source,
            destination,
            ct: TestContext.Current.CancellationToken));

        Assert.True(File.Exists(sourcePath));
        Assert.False(File.Exists(Path.Combine(destinationDirectory, "readonly.txt")));
    }

    [Theory]
    [InlineData(false, "report_副本.txt", "report_副本2.txt")]
    [InlineData(true, "report (1).txt", "report (2).txt")]
    public async Task CopyAsync_NameCollisionCreatesPlatformStyleCopies(
        bool windowsNames,
        string firstCopy,
        string secondCopy)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_root, $"copies-{windowsNames}")).FullName;
        var sourcePath = Path.Combine(directory, "report.txt");
        await File.WriteAllTextAsync(sourcePath, "original", TestContext.Current.CancellationToken);
        var provider = new LocalFileSystemProvider();
        var source = CreateFile(provider, sourcePath, ResourceCapabilities.Read | ResourceCapabilities.Delete);
        var destination = new ContainerRef(
            new ResourceRef(provider, directory),
            ContainerCapabilities.AcceptFiles);
        var service = new ResourceTransferService(windowsNames);

        await service.CopyAsync(source, destination, ct: TestContext.Current.CancellationToken);
        await service.CopyAsync(source, destination, ct: TestContext.Current.CancellationToken);

        Assert.Equal("original", await File.ReadAllTextAsync(
            Path.Combine(directory, firstCopy), TestContext.Current.CancellationToken));
        Assert.Equal("original", await File.ReadAllTextAsync(
            Path.Combine(directory, secondCopy), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopyAsync_TarGZipCollisionKeepsTheCompoundExtensionTogether()
    {
        var directory = Directory.CreateDirectory(Path.Combine(_root, "tar-gzip-copy")).FullName;
        var sourcePath = Path.Combine(directory, "aaa.tar.gz");
        await File.WriteAllTextAsync(sourcePath, "archive", TestContext.Current.CancellationToken);
        var provider = new LocalFileSystemProvider();
        var source = CreateFile(provider, sourcePath, ResourceCapabilities.Read);
        var destination = new ContainerRef(
            new ResourceRef(provider, directory), ContainerCapabilities.AcceptFiles);

        await new ResourceTransferService(windowsCopyNames: false).CopyAsync(
            source, destination, ct: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(directory, "aaa_副本.tar.gz")));
    }

    [Fact]
    public async Task CopyAsync_UsesDestinationProvidersCaseSensitiveNameComparer()
    {
        var sourcePath = Path.Combine(_root, "report.txt");
        await File.WriteAllTextAsync(sourcePath, "data", TestContext.Current.CancellationToken);
        var local = new LocalFileSystemProvider();
        var source = CreateFile(local, sourcePath, ResourceCapabilities.Read);
        var remote = new CaseSensitiveDestinationProvider();

        await new ResourceTransferService().CopyAsync(
            source,
            new ContainerRef(new ResourceRef(remote, "/target"), ContainerCapabilities.AcceptFiles),
            ct: TestContext.Current.CancellationToken);

        Assert.Equal("report.txt", remote.WrittenName);
    }

    private static BrowserItem CreateFile(
        IFileSystemProvider provider,
        string path,
        ResourceCapabilities capabilities) => new(
        new FileItemModel
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            Extension = FileNameUtility.GetExtension(Path.GetFileName(path)),
            ItemType = FileSystemItemType.File,
            CanRead = capabilities.HasFlag(ResourceCapabilities.Read),
            CanWrite = capabilities.HasFlag(ResourceCapabilities.Delete),
        },
        new ResourceRef(provider, path),
        new ResourceRef(provider, Path.GetDirectoryName(path)!),
        capabilities);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class RecordingTrashService : ITrashService
    {
        public List<string> Paths { get; } = new();
        public void MoveToTrash(string path) => Paths.Add(path);
    }

    private sealed class CaseSensitiveDestinationProvider : IFileSystemProvider, IWritableResourceProvider
    {
        public string Id => "remote:case-sensitive";
        public string? WrittenName { get; private set; }
        public bool TracksHistory => true;
        public bool CanEnter(IFileSystemItem item) => item.ItemType == FileSystemItemType.Directory;
        public Task<Stream> OpenReadAsync(string path, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<IFileSystemItem>> ListChildrenAsync(string path, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<IFileSystemItem>>([
                new FileItemModel { Name = "Report.txt", FullPath = "/target/Report.txt" },
            ]);
        public Task<ResourceRef> CreateDirectoryResourceAsync(ResourceRef parent, string name, CancellationToken ct = default) =>
            throw new NotSupportedException();
        public Task<Stream> OpenWriteAsync(ResourceRef parent, string name, CancellationToken ct = default)
        {
            WrittenName = name;
            return Task.FromResult<Stream>(new MemoryStream());
        }
    }
}
