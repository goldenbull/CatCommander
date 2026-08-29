using System.Formats.Tar;
using System.IO.Compression;
using CatCommander.Browsing;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;
using DiscUtils.Iso9660;

namespace CatCommander.Tests.FileSystem;

public sealed class ArchiveFileSystemProviderTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"CatCommanderArchives_{Guid.NewGuid():N}");
    public ArchiveFileSystemProviderTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Zip_ExposesSyntheticDirectoriesAndStreamsFileContents()
    {
        var path = Path.Combine(_root, "sample.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("folder/nested.txt");
            await using var output = entry.Open();
            await output.WriteAsync("hello"u8.ToArray(), TestContext.Current.CancellationToken);
        }
        var provider = CreateArchiveProvider(path);

        var rootItems = await provider.ListChildrenAsync("/", TestContext.Current.CancellationToken);
        var folder = Assert.Single(rootItems);
        Assert.Equal(FileSystemItemType.Directory, folder.ItemType);
        var nested = Assert.Single(await provider.ListChildrenAsync(folder.FullPath, TestContext.Current.CancellationToken));
        await using var input = await provider.OpenReadAsync(nested.FullPath, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(input);
        Assert.Equal("hello", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TarGZip_IsReadAsTarTreeWithoutTemporaryExtraction()
    {
        var path = Path.Combine(_root, "sample.tar.gz");
        await using (var file = File.Create(path))
        await using (var gzip = new GZipStream(file, CompressionMode.Compress))
        await using (var tar = new TarWriter(gzip, leaveOpen: false))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "inside.txt")
            {
                DataStream = new MemoryStream("tar-data"u8.ToArray()),
            };
            await tar.WriteEntryAsync(entry, TestContext.Current.CancellationToken);
        }

        var provider = CreateArchiveProvider(path);
        var item = Assert.Single(await provider.ListChildrenAsync("/", TestContext.Current.CancellationToken));
        Assert.Equal("inside.txt", item.Name);
        Assert.False(Directory.EnumerateDirectories(_root).Any());
    }

    [Fact]
    public async Task PlainGZip_AppearsAsSingleFileVirtualDirectory()
    {
        var path = Path.Combine(_root, "single.txt.gz");
        await using (var file = File.Create(path))
        await using (var gzip = new GZipStream(file, CompressionMode.Compress))
            await gzip.WriteAsync("single"u8.ToArray(), TestContext.Current.CancellationToken);

        var provider = CreateArchiveProvider(path);
        var item = Assert.Single(await provider.ListChildrenAsync("/", TestContext.Current.CancellationToken));

        Assert.Equal(FileSystemItemType.File, item.ItemType);
        await using var input = await provider.OpenReadAsync(item.FullPath, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(input);
        Assert.Equal("single", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task F5StyleCrossProviderCopy_ExtractsArchiveDirectory()
    {
        var path = Path.Combine(_root, "copy.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("folder/a.txt");
            await using var output = entry.Open();
            await output.WriteAsync("copied"u8.ToArray(), TestContext.Current.CancellationToken);
        }
        var archiveProvider = CreateArchiveProvider(path);
        var folder = Assert.Single(await archiveProvider.ListChildrenAsync("/", TestContext.Current.CancellationToken));
        var source = new BrowserItem(
            folder,
            new ResourceRef(archiveProvider, folder.FullPath),
            new ResourceRef(archiveProvider, "/"),
            ResourceCapabilities.Read | ResourceCapabilities.EnumerateChildren);
        var destinationPath = Directory.CreateDirectory(Path.Combine(_root, "destination")).FullName;
        var local = new LocalFileSystemProvider();
        var destination = new ContainerRef(
            new ResourceRef(local, destinationPath),
            ((IFileSystemProvider)local).ContainerCapabilities);

        await new ResourceTransferService().CopyAsync(source, destination, ct: TestContext.Current.CancellationToken);

        Assert.Equal("copied", await File.ReadAllTextAsync(
            Path.Combine(destinationPath, "folder", "a.txt"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Iso_ListsAndReadsFiles()
    {
        var path = Path.Combine(_root, "sample.iso");
        var builder = new CDBuilder { UseJoliet = true, VolumeIdentifier = "TEST" };
        builder.AddFile("folder\\iso.txt", "iso-data"u8.ToArray());
        builder.Build(path);
        var local = new LocalFileSystemProvider();
        var provider = new IsoFileSystemProvider(
            path,
            new ResourceRef(local, path),
            new ResourceRef(local, _root));

        var folder = Assert.Single(await provider.ListChildrenAsync("/", TestContext.Current.CancellationToken));
        var file = Assert.Single(await provider.ListChildrenAsync(folder.FullPath, TestContext.Current.CancellationToken));
        await using var input = await provider.OpenReadAsync(file.FullPath, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(input);
        Assert.Equal("iso-data", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Factory_UsesInjectedBackingProviderWhenLeavingArchiveRoot()
    {
        var path = Path.Combine(_root, "backing.zip");
        using (ZipFile.Open(path, ZipArchiveMode.Create)) { }
        var local = new LocalFileSystemProvider();
        var provider = new ArchiveFileSystemProviderFactory(
            new ProviderCredentialStore(), local).Create(path);
        var root = new ResourceRef(provider, "/");

        var parent = provider.GetParentResource(root);
        Assert.True(parent.HasValue);
        Assert.Same(local, parent.Value.Provider);
        Assert.Equal(_root, parent.Value.Path);
        Assert.Same(local, provider.GetParentSelectionResource(root).Provider);
    }

    private ArchiveFileSystemProvider CreateArchiveProvider(string path)
    {
        var local = new LocalFileSystemProvider();
        return new ArchiveFileSystemProvider(
            path,
            new ProviderCredentialStore(),
            new ResourceRef(local, path),
            new ResourceRef(local, Path.GetDirectoryName(path)!));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
