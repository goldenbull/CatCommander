using System;
using System.Linq;
using CatCommander.Utils;

namespace CatCommander.Utils;

/// <summary>
/// Example usage of ArchiveFileHelper for working with zip archives
/// </summary>
public static class ArchiveFileHelperExample
{
    /// <summary>
    /// Example: Load and explore a zip archive
    /// </summary>
    public static void Example1_LoadArchive()
    {
        var archivePath = "/path/to/archive.zip";

        Console.WriteLine($"=== Loading Archive: {archivePath} ===\n");

        // Load the archive and build tree structure
        var tree = ArchiveFileHelper.LoadArchive(archivePath);

        if (tree == null)
        {
            Console.WriteLine("Failed to load archive");
            return;
        }

        // Display root level items
        Console.WriteLine("Root level items:");
        var rootItems = tree.GetItemsAtPath("", '/');
        foreach (var item in rootItems)
        {
            var icon = item.IsDirectory ? "📁" : "📄";
            var size = item.Item?.DisplaySize ?? "";
            Console.WriteLine($"  {icon} {item.Name,-30} {size}");
        }

        Console.WriteLine($"\nTotal files: {tree.GetAllFiles().Count()}");
        Console.WriteLine($"Total directories: {tree.GetAllDirectories().Count()}");
    }

    /// <summary>
    /// Example: Get archive information
    /// </summary>
    public static void Example2_ArchiveInfo()
    {
        var archivePath = "/path/to/archive.zip";

        Console.WriteLine($"=== Archive Information ===\n");

        var info = ArchiveFileHelper.GetArchiveInfo(archivePath);

        if (info == null)
        {
            Console.WriteLine("Failed to get archive info");
            return;
        }

        Console.WriteLine($"Archive: {info.ArchivePath}");
        Console.WriteLine($"Total Entries: {info.TotalEntries}");
        Console.WriteLine($"Files: {info.FileCount}");
        Console.WriteLine($"Directories: {info.DirectoryCount}");
        Console.WriteLine($"Uncompressed Size: {info.FormattedUncompressedSize}");
        Console.WriteLine($"Compressed Size: {info.FormattedCompressedSize}");
        Console.WriteLine($"Compression Ratio: {info.CompressionRatio:F1}%");
        Console.WriteLine($"\nSummary: {info.Summary}");
    }

    /// <summary>
    /// Example: Navigate through archive structure
    /// </summary>
    public static void Example3_NavigateArchive()
    {
        var archivePath = "/path/to/archive.zip";

        Console.WriteLine($"=== Navigating Archive Structure ===\n");

        var tree = ArchiveFileHelper.LoadArchive(archivePath);
        if (tree == null)
            return;

        // Navigate to a specific path
        var path = "subfolder/docs";
        Console.WriteLine($"Contents of '{path}':");

        var items = tree.GetItemsAtPath(path, '/');
        foreach (var item in items)
        {
            var icon = item.IsDirectory ? "📁" : "📄";
            var modified = item.Item?.Modified.ToString("yyyy-MM-dd HH:mm:ss") ?? "";
            Console.WriteLine($"  {icon} {item.Name,-30} {modified}");
        }
    }

    /// <summary>
    /// Example: Find specific files in archive
    /// </summary>
    public static void Example4_FindFiles()
    {
        var archivePath = "/path/to/archive.zip";

        Console.WriteLine($"=== Finding Files in Archive ===\n");

        var tree = ArchiveFileHelper.LoadArchive(archivePath);
        if (tree == null)
            return;

        // Find all .txt files
        var txtFiles = tree.GetAllFiles()
            .Where(n => n.Item?.Extension == ".txt")
            .ToList();

        Console.WriteLine($"Found {txtFiles.Count} .txt files:");
        foreach (var file in txtFiles)
        {
            Console.WriteLine($"  📄 {file.FullPath}");
        }

        // Find all images
        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
        var images = tree.GetAllFiles()
            .Where(n => imageExtensions.Contains(n.Item?.Extension.ToLowerInvariant()))
            .ToList();

        Console.WriteLine($"\nFound {images.Count} image files:");
        foreach (var image in images.Take(10)) // Show first 10
        {
            Console.WriteLine($"  🖼️  {image.FullPath} ({image.Item?.DisplaySize})");
        }
    }

    /// <summary>
    /// Example: Check if file is an archive
    /// </summary>
    public static void Example5_CheckArchiveType()
    {
        Console.WriteLine("=== Checking Archive Types ===\n");

        var files = new[]
        {
            "document.zip",
            "backup.7z",
            "data.tar.gz",
            "image.jpg",
            "text.txt",
            "package.rar"
        };

        foreach (var file in files)
        {
            var isArchive = ArchiveFileHelper.IsArchiveFile(file);
            var icon = isArchive ? "📦" : "📄";
            var type = isArchive ? "Archive" : "Regular file";
            Console.WriteLine($"{icon} {file,-20} - {type}");
        }
    }

    /// <summary>
    /// Example: Extract all entries as flat list
    /// </summary>
    public static void Example6_ExtractEntriesList()
    {
        var archivePath = "/path/to/archive.zip";

        Console.WriteLine($"=== Extracting Archive Entries as Flat List ===\n");

        var entries = ArchiveFileHelper.ExtractArchiveEntries(archivePath);

        Console.WriteLine($"Extracted {entries.Count} entries:\n");

        // Show first 20 entries
        foreach (var entry in entries.Take(20))
        {
            var icon = entry.ItemType == Models.FileSystemItemType.Directory ? "📁" : "📄";
            Console.WriteLine($"{icon} {entry.FullPath,-50} {entry.DisplaySize,10}");
        }

        if (entries.Count > 20)
        {
            Console.WriteLine($"\n... and {entries.Count - 20} more entries");
        }
    }

    /// <summary>
    /// Example: Build custom tree and query it
    /// </summary>
    public static void Example7_CustomTreeQuery()
    {
        var archivePath = "/path/to/archive.zip";

        Console.WriteLine($"=== Custom Tree Queries ===\n");

        var tree = ArchiveFileHelper.LoadArchive(archivePath);
        if (tree == null)
            return;

        // Find deepest path
        var allFiles = tree.GetAllFiles().ToList();
        var deepestFile = allFiles
            .OrderByDescending(f => f.FullPath.Count(c => c == '/'))
            .FirstOrDefault();

        if (deepestFile != null)
        {
            var depth = deepestFile.FullPath.Count(c => c == '/') + 1;
            Console.WriteLine($"Deepest file (depth {depth}): {deepestFile.FullPath}");
        }

        // Find largest file
        var largestFile = allFiles
            .OrderByDescending(f => f.Item?.Size ?? 0)
            .FirstOrDefault();

        if (largestFile?.Item != null)
        {
            Console.WriteLine($"Largest file: {largestFile.FullPath} ({largestFile.Item.DisplaySize})");
        }

        // Find newest file
        var newestFile = allFiles
            .OrderByDescending(f => f.Item?.Modified ?? DateTime.MinValue)
            .FirstOrDefault();

        if (newestFile?.Item != null)
        {
            Console.WriteLine($"Newest file: {newestFile.FullPath} ({newestFile.Item.Modified:yyyy-MM-dd})");
        }
    }
}
