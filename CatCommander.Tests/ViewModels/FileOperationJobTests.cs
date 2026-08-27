using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.ViewModels;
using Xunit;

namespace CatCommander.Tests.ViewModels;

public class FileOperationJobTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileSystemProvider _provider = new();

    public FileOperationJobTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "CatCommanderFileOperationJobTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // RunAsync posts every property mutation through Dispatcher.UIThread (see its own doc
    // comment) - draining the queue after awaiting it is what makes Status/CompletedCount/etc.
    // actually reflect the finished job by the time assertions run.
    private static void Pump()
    {
        for (var i = 0; i < 50; i++)
            Dispatcher.UIThread.RunJobs();
    }

    private static FileItemModel FileItem(string path) =>
        new() { Name = Path.GetFileName(path), FullPath = path, ItemType = FileSystemItemType.File };

    [AvaloniaFact]
    public async Task RunAsync_Delete_DeletesEveryTargetFile()
    {
        var a = Path.Combine(_root, "a.txt");
        var b = Path.Combine(_root, "b.txt");
        File.WriteAllText(a, "hello");
        File.WriteAllText(b, "world");

        var job = new FileOperationJob(FileOperationKind.Delete, new[] { FileItem(a), FileItem(b) }, destination: null, _provider);
        await job.RunAsync();
        Pump();

        Assert.False(File.Exists(a));
        Assert.False(File.Exists(b));
        Assert.Equal(FileOperationJobStatus.Completed, job.Status);
        Assert.Equal(2, job.CompletedCount);
    }

    [AvaloniaFact]
    public async Task RunAsync_Delete_RemovesADirectory_Recursively()
    {
        var dir = Path.Combine(_root, "subdir");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "inside.txt"), "contents");
        var dirItem = new FileItemModel { Name = "subdir", FullPath = dir, ItemType = FileSystemItemType.Directory };

        var job = new FileOperationJob(FileOperationKind.Delete, new[] { dirItem }, destination: null, _provider);
        await job.RunAsync();
        Pump();

        Assert.False(Directory.Exists(dir));
        Assert.Equal(FileOperationJobStatus.Completed, job.Status);
    }

    [Fact]
    public void Description_ForDelete_MentionsNoDestination()
    {
        var job = new FileOperationJob(FileOperationKind.Delete, new[] { FileItem(Path.Combine(_root, "a.txt")) }, destination: null, _provider);

        Assert.Equal("Delete 1 item", job.Description);
    }

    [Fact]
    public void Description_ForCopy_MentionsTheDestination()
    {
        var job = new FileOperationJob(FileOperationKind.Copy, new[] { FileItem(Path.Combine(_root, "a.txt")) }, destination: "/some/dest", _provider);

        Assert.Equal("Copy 1 item to /some/dest", job.Description);
    }
}
