using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CatCommander.FileSystem;
using Xunit;

namespace CatCommander.Tests.FileSystem;

public class LocalFileSystemProviderTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileSystemProvider _provider = new();

    public LocalFileSystemProviderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderLFSPTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string NewDir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task CreateDirectoryAsync_CreatesTheDirectory_AndReturnsItsFullPath()
    {
        var result = await _provider.CreateDirectoryAsync(_root, "newFolder", TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_root, "newFolder"), result);
        Assert.True(Directory.Exists(result));
    }

    [Fact]
    public async Task RenameAsync_RenamesAFile_InPlace()
    {
        var original = Path.Combine(_root, "a.txt");
        File.WriteAllText(original, "hello");

        var newPath = await _provider.RenameAsync(original, "b.txt", TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_root, "b.txt"), newPath);
        Assert.False(File.Exists(original));
        Assert.Equal("hello", File.ReadAllText(newPath));
    }

    [Fact]
    public async Task RenameAsync_RenamesADirectory_InPlace_KeepingItsContents()
    {
        var original = NewDir("oldName");
        File.WriteAllText(Path.Combine(original, "inside.txt"), "contents");

        var newPath = await _provider.RenameAsync(original, "newName", TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(_root, "newName"), newPath);
        Assert.False(Directory.Exists(original));
        Assert.True(File.Exists(Path.Combine(newPath, "inside.txt")));
    }

    [Fact]
    public async Task CopyAsync_CopiesASingleFile_LeavingTheSourceInPlace()
    {
        var source = Path.Combine(_root, "a.txt");
        File.WriteAllText(source, "hello");
        var destinationDir = NewDir("dest");

        await _provider.CopyAsync(source, destinationDir, progress: null, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(source));
        Assert.Equal("hello", File.ReadAllText(Path.Combine(destinationDir, "a.txt")));
    }

    [Fact]
    public async Task CopyAsync_CopiesADirectory_Recursively()
    {
        var source = NewDir("srcTree");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "top.txt"), "top");
        File.WriteAllText(Path.Combine(source, "nested", "deep.txt"), "deep");
        var destinationDir = NewDir("dest");

        await _provider.CopyAsync(source, destinationDir, progress: null, TestContext.Current.CancellationToken);

        var copiedRoot = Path.Combine(destinationDir, "srcTree");
        Assert.Equal("top", File.ReadAllText(Path.Combine(copiedRoot, "top.txt")));
        Assert.Equal("deep", File.ReadAllText(Path.Combine(copiedRoot, "nested", "deep.txt")));
        // Source untouched by a Copy.
        Assert.True(File.Exists(Path.Combine(source, "top.txt")));
    }

    [Fact]
    public async Task CopyAsync_ReportsProgress_OncePerFileCopied()
    {
        var source = NewDir("srcTree");
        Directory.CreateDirectory(Path.Combine(source, "nested"));
        File.WriteAllText(Path.Combine(source, "top.txt"), "top");
        File.WriteAllText(Path.Combine(source, "nested", "deep.txt"), "deep");
        var destinationDir = NewDir("dest");

        var reported = 0;
        var progress = new Progress<string>(_ => Interlocked.Increment(ref reported));

        await _provider.CopyAsync(source, destinationDir, progress, TestContext.Current.CancellationToken);

        // Progress<T> marshals its callback asynchronously - give it a moment to catch up.
        for (var i = 0; i < 50 && reported < 2; i++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        Assert.Equal(2, reported); // top.txt + nested/deep.txt, not the directory itself.
    }

    [Fact]
    public async Task CopyAsync_OverwritesAnExistingFile_AtTheDestination()
    {
        var source = Path.Combine(_root, "a.txt");
        File.WriteAllText(source, "new content");
        var destinationDir = NewDir("dest");
        File.WriteAllText(Path.Combine(destinationDir, "a.txt"), "old content");

        await _provider.CopyAsync(source, destinationDir, progress: null, TestContext.Current.CancellationToken);

        Assert.Equal("new content", File.ReadAllText(Path.Combine(destinationDir, "a.txt")));
    }

    [Fact]
    public async Task MoveAsync_MovesAFile_RemovingTheSource()
    {
        var source = Path.Combine(_root, "a.txt");
        File.WriteAllText(source, "hello");
        var destinationDir = NewDir("dest");

        await _provider.MoveAsync(source, destinationDir, progress: null, TestContext.Current.CancellationToken);

        var newPath = Path.Combine(destinationDir, "a.txt");
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(newPath));
        Assert.Equal("hello", File.ReadAllText(newPath));
    }

    [Fact]
    public async Task MoveAsync_MovesADirectory_Recursively_RemovingTheSource()
    {
        var source = NewDir("srcTree");
        File.WriteAllText(Path.Combine(source, "inside.txt"), "contents");
        var destinationDir = NewDir("dest");

        await _provider.MoveAsync(source, destinationDir, progress: null, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(destinationDir, "srcTree", "inside.txt")));
    }

    [Fact]
    public async Task DeleteAsync_DeletesAFile()
    {
        var path = Path.Combine(_root, "a.txt");
        File.WriteAllText(path, "hello");

        await _provider.DeleteAsync(path, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task DeleteAsync_DeletesADirectory_Recursively()
    {
        var dir = NewDir("srcTree");
        Directory.CreateDirectory(Path.Combine(dir, "nested"));
        File.WriteAllText(Path.Combine(dir, "top.txt"), "top");
        File.WriteAllText(Path.Combine(dir, "nested", "deep.txt"), "deep");

        await _provider.DeleteAsync(dir, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public async Task ListChildrenAsync_MarksDotPrefixedEntries_AsHidden()
    {
        // Cmd/Ctrl+. (ItemBrowserViewModel.ToggleHiddenFiles) needs this to be true regardless of
        // platform - a leading '.' is the only signal macOS/Linux ever have for "hidden".
        File.WriteAllText(Path.Combine(_root, ".hidden.txt"), "secret");
        File.WriteAllText(Path.Combine(_root, "visible.txt"), "public");
        NewDir(".hiddenDir");
        NewDir("visibleDir");

        var items = await _provider.ListChildrenAsync(_root, TestContext.Current.CancellationToken);

        Assert.True(items.Single(i => i.Name == ".hidden.txt").IsHidden);
        Assert.False(items.Single(i => i.Name == "visible.txt").IsHidden);
        Assert.True(items.Single(i => i.Name == ".hiddenDir").IsHidden);
        Assert.False(items.Single(i => i.Name == "visibleDir").IsHidden);
    }

    [Fact]
    public async Task ListChildrenAsync_MarksAnEntryWithTheHiddenAttribute_AsHidden_EvenWithoutADotPrefix()
    {
        var path = Path.Combine(_root, "secret.txt");
        File.WriteAllText(path, "secret");
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);

        var items = await _provider.ListChildrenAsync(_root, TestContext.Current.CancellationToken);

        Assert.True(items.Single(i => i.Name == "secret.txt").IsHidden);
    }
}
