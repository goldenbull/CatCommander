using System;
using System.Collections.Generic;
using CatCommander.Models;
using CatCommander.Utils;

namespace CatCommander.Utils;

/// <summary>
/// Example usage of FileItemTreeNode for reconstructing directory structure from flat lists
/// </summary>
public static class FileItemTreeNodeExample
{
    /// <summary>
    /// Example: Building tree from zip archive entries (IFileSystemItem list)
    /// </summary>
    public static void Example1_ZipArchive()
    {
        // Simulate items read from a zip archive (flattened list with '/' separators)
        var zipItems = new List<IFileSystemItem>
        {
            new FileItemModel
            {
                Name = "file1.txt",
                FullPath = "folder1/file1.txt",
                ItemType = FileSystemItemType.File,
                Size = 1024
            },
            new FileItemModel
            {
                Name = "file2.txt",
                FullPath = "folder1/subfolder1/file2.txt",
                ItemType = FileSystemItemType.File,
                Size = 2048
            },
            new FileItemModel
            {
                Name = "file3.txt",
                FullPath = "folder1/subfolder1/file3.txt",
                ItemType = FileSystemItemType.File,
                Size = 512
            },
            new FileItemModel
            {
                Name = "file4.txt",
                FullPath = "folder1/subfolder2/file4.txt",
                ItemType = FileSystemItemType.File,
                Size = 768
            },
            new FileItemModel
            {
                Name = "root-file.txt",
                FullPath = "root-file.txt",
                ItemType = FileSystemItemType.File,
                Size = 256
            }
        };

        // Build tree from flat list
        var tree = FileItemTreeNode.CreateFrom(zipItems);

        // Get all items at root level
        var rootItems = tree.GetItemsAtPath("");
        Console.WriteLine("Root level items:");
        foreach (var item in rootItems)
        {
            var type = item.IsDirectory ? "[DIR]" : "[FILE]";
            Console.WriteLine($"  {type} {item.Name}");
        }

        // Get items in folder1
        var folder1Items = tree.GetItemsAtPath("folder1");
        Console.WriteLine("\nItems in folder1:");
        foreach (var item in folder1Items)
        {
            var type = item.IsDirectory ? "[DIR]" : "[FILE]";
            Console.WriteLine($"  {type} {item.Name}");
        }

        // Get items in nested path
        var nestedItems = tree.GetItemsAtPath("folder1/subfolder1");
        Console.WriteLine("\nItems in folder1/subfolder1:");
        foreach (var item in nestedItems)
        {
            Console.WriteLine($"  [FILE] {item.Name}");
        }

        // Find a specific node
        var node = tree.FindNode("folder1/subfolder1");
        if (node != null)
        {
            Console.WriteLine($"\nFound node: {node.FullPath}");
            Console.WriteLine($"Has {node.Children.Count} children");
        }
    }

    /// <summary>
    /// Example: Navigating like a file browser through archive contents
    /// </summary>
    public static void Example2_FileBrowserSimulation()
    {
        var items = new List<IFileSystemItem>
        {
            new FileItemModel
            {
                Name = "file1.txt",
                FullPath = "Documents/Work/Project1/file1.txt",
                ItemType = FileSystemItemType.File,
                Size = 1024
            },
            new FileItemModel
            {
                Name = "file2.txt",
                FullPath = "Documents/Work/Project1/file2.txt",
                ItemType = FileSystemItemType.File,
                Size = 2048
            },
            new FileItemModel
            {
                Name = "readme.md",
                FullPath = "Documents/Work/Project2/readme.md",
                ItemType = FileSystemItemType.File,
                Size = 512
            },
            new FileItemModel
            {
                Name = "photo1.jpg",
                FullPath = "Documents/Personal/photo1.jpg",
                ItemType = FileSystemItemType.File,
                Size = 204800
            },
            new FileItemModel
            {
                Name = "installer.exe",
                FullPath = "Downloads/installer.exe",
                ItemType = FileSystemItemType.File,
                Size = 5242880
            }
        };

        // Build tree from flat list
        var tree = FileItemTreeNode.CreateFrom(items);

        // Simulate browsing starting from root
        Console.WriteLine("\n=== File Browser Simulation ===");

        var currentPath = "";
        Console.WriteLine($"Current path: /{currentPath}");
        ShowCurrentDirectory(tree, currentPath);

        // Navigate to Documents
        currentPath = "Documents";
        Console.WriteLine($"\nNavigating to: /{currentPath}");
        ShowCurrentDirectory(tree, currentPath);

        // Navigate to Documents/Work
        currentPath = "Documents/Work";
        Console.WriteLine($"\nNavigating to: /{currentPath}");
        ShowCurrentDirectory(tree, currentPath);

        // Navigate to Documents/Work/Project1
        currentPath = "Documents/Work/Project1";
        Console.WriteLine($"\nNavigating to: /{currentPath}");
        ShowCurrentDirectory(tree, currentPath);
    }

    /// <summary>
    /// Example: Working with virtual/intermediate directories
    /// </summary>
    public static void Example3_VirtualDirectories()
    {
        // Only files are in the flat list, intermediate directories are created automatically
        var items = new List<IFileSystemItem>
        {
            new FileItemModel
            {
                Name = "config.json",
                FullPath = "src/data/config.json",
                ItemType = FileSystemItemType.File,
                Size = 512
            },
            new FileItemModel
            {
                Name = "settings.xml",
                FullPath = "src/data/settings.xml",
                ItemType = FileSystemItemType.File,
                Size = 1024
            }
        };

        // Build tree from flat list
        var tree = FileItemTreeNode.CreateFrom(items);

        // Navigate to 'src' - this is a virtual directory (not in the original list)
        var srcNode = tree.FindNode("src");
        if (srcNode != null)
        {
            Console.WriteLine($"\nVirtual directory 'src':");
            Console.WriteLine($"  Path: {srcNode.FullPath}");
            Console.WriteLine($"  IsDirectory: {srcNode.IsDirectory}");
            Console.WriteLine($"  Has Item: {srcNode.Item != null}"); // False - virtual directory
            Console.WriteLine($"  Children count: {srcNode.Children.Count}");
        }

        // Navigate to 'src/data' - another virtual directory
        var dataNode = tree.FindNode("src/data");
        if (dataNode != null)
        {
            Console.WriteLine($"\nVirtual directory 'src/data':");
            Console.WriteLine($"  Children:");
            foreach (var child in dataNode.Children.Values)
            {
                Console.WriteLine($"    {child.Name} (Has Item: {child.Item != null})");
            }
        }
    }

    private static void ShowCurrentDirectory(FileItemTreeNode itemTree, string path)
    {
        var items = itemTree.GetItemsAtPath(path);
        foreach (var item in items)
        {
            var type = item.IsDirectory ? "[DIR]" : "[FILE]";
            Console.WriteLine($"  {type} {item.Name}");
        }
    }
}
