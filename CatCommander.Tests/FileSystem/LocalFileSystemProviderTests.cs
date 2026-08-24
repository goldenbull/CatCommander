using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CatCommander.FileSystem;
using CatCommander.Models;
using Xunit;

namespace CatCommander.Tests.FileSystem;

public class LocalFileSystemProviderTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileSystemProvider _provider = new();

    public LocalFileSystemProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Path.Combine(_root, "subdir"));
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(_root, "b.txt"), "world!!");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task ListChildrenAsync_ReturnsFilesAndDirectories()
    {
        var items = await _provider.ListChildrenAsync(_root, TestContext.Current.CancellationToken);

        var dir = Assert.Single(items, i => i.ItemType == FileSystemItemType.Directory);
        Assert.Equal("subdir", dir.Name);

        var fileNames = items.Where(i => i.ItemType == FileSystemItemType.File).Select(i => i.Name).ToList();
        Assert.Contains("a.txt", fileNames);
        Assert.Contains("b.txt", fileNames);
    }

    [Fact]
    public async Task ListChildrenAsync_ReportsCorrectFileSize()
    {
        var items = await _provider.ListChildrenAsync(_root, TestContext.Current.CancellationToken);
        var b = items.Single(i => i.Name == "b.txt");
        Assert.Equal(7, b.Size);
    }

    [Fact]
    public async Task OpenReadAsync_ReadsFileContent()
    {
        await using var stream = await _provider.OpenReadAsync(Path.Combine(_root, "a.txt"), TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        Assert.Equal("hello", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CanEnter_ReturnsTrueForDirectory_FalseForFile()
    {
        var items = await _provider.ListChildrenAsync(_root, TestContext.Current.CancellationToken);
        Assert.True(_provider.CanEnter(items.Single(i => i.Name == "subdir")));
        Assert.False(_provider.CanEnter(items.Single(i => i.Name == "a.txt")));
    }
}
