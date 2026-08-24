using System;
using System.Collections.Generic;
using System.Linq;
using CatCommander.Models;

namespace CatCommander.Utils;

/// <summary>
/// Represents a node in a hierarchical file tree structure.
/// Can be built from a flattened list of IFileSystemItem objects.
/// Useful for reconstructing directory structure from zip archives, SFTP listings, etc.
/// </summary>
public class FileItemTreeNode
{
    private FileItemTreeNode(string path)
    {
        FullPath = path;
        Children = new Dictionary<string, FileItemTreeNode>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The underlying file system item (null for intermediate/virtual directories)
    /// </summary>
    public IFileSystemItem? Item { get; private set; }

    /// <summary>
    /// Full path of this node
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Name of this node (last component of path)
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this node represents a directory
    /// </summary>
    public bool IsDirectory => Item == null || Item.ItemType == FileSystemItemType.Directory;

    /// <summary>
    /// Child nodes indexed by name
    /// </summary>
    public Dictionary<string, FileItemTreeNode> Children { get; }

    /// <summary>
    /// Whether this node has any children
    /// </summary>
    public bool HasChildren => Children.Count > 0;

    /// <summary>
    /// Creates a tree structure from a flat list of file system items
    /// </summary>
    /// <param name="items">Flattened list of items with full paths</param>
    /// <param name="separator">Path separator character (default: '/')</param>
    /// <returns>Root node containing the tree structure</returns>
    public static FileItemTreeNode CreateFrom(IEnumerable<IFileSystemItem> items, char separator = '/')
    {
        var root = new FileItemTreeNode(string.Empty);

        foreach (var item in items)
        {
            AddItemToTree(root, item, separator);
        }

        return root;
    }

    /// <summary>
    /// Gets all items at a specific path in the tree
    /// </summary>
    /// <param name="path">Path to query (empty for root)</param>
    /// <param name="separator">Path separator character (default: '/')</param>
    /// <returns>List of items at the specified path</returns>
    public IEnumerable<FileItemTreeNode> GetItemsAtPath(string path, char separator = '/')
    {
        if (string.IsNullOrEmpty(path))
        {
            return Children.Values;
        }

        var parts = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var current = this;

        foreach (var part in parts)
        {
            if (!current.Children.TryGetValue(part, out var child))
            {
                return Enumerable.Empty<FileItemTreeNode>();
            }
            current = child;
        }

        return current.Children.Values;
    }

    /// <summary>
    /// Finds a node at the specified relative path
    /// </summary>
    public FileItemTreeNode? FindNode(string relativePath, char separator = '/')
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            return this;
        }

        var parts = relativePath.Split(separator, StringSplitOptions.RemoveEmptyEntries);
        var current = this;

        foreach (var part in parts)
        {
            if (!current.Children.TryGetValue(part, out var child))
            {
                return null;
            }
            current = child;
        }

        return current;
    }

    /// <summary>
    /// Gets all descendant items recursively
    /// </summary>
    public IEnumerable<FileItemTreeNode> GetAllDescendants()
    {
        foreach (var child in Children.Values)
        {
            yield return child;

            foreach (var descendant in child.GetAllDescendants())
            {
                yield return descendant;
            }
        }
    }

    /// <summary>
    /// Gets all file items (non-directories) recursively
    /// </summary>
    public IEnumerable<FileItemTreeNode> GetAllFiles()
    {
        return GetAllDescendants().Where(n => !n.IsDirectory);
    }

    /// <summary>
    /// Gets all directory items recursively
    /// </summary>
    public IEnumerable<FileItemTreeNode> GetAllDirectories()
    {
        return GetAllDescendants().Where(n => n.IsDirectory);
    }

    private static void AddItemToTree(FileItemTreeNode root, IFileSystemItem item, char separator)
    {
        var path = item.FullPath.Replace('\\', separator); // Normalize separators
        var parts = path.Split(separator, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return;
        }

        var current = root;
        var currentPath = string.Empty;

        // Traverse/create intermediate directories
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var part = parts[i];
            currentPath = string.IsNullOrEmpty(currentPath) ? part : $"{currentPath}{separator}{part}";

            if (!current.Children.TryGetValue(part, out var child))
            {
                // Create intermediate directory node (no item, just a virtual directory)
                child = new FileItemTreeNode(currentPath)
                {
                    Name = part
                };
                current.Children[part] = child;
            }

            current = child;
        }

        // Add the final item (file or directory)
        var finalPart = parts[^1];
        if (!current.Children.ContainsKey(finalPart))
        {
            current.Children[finalPart] = new FileItemTreeNode(item.FullPath)
            {
                Name = item.Name,
                Item = item
            };
        }
    }
}
