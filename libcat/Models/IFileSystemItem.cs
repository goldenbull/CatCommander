namespace CatCommander.Models;

/// <summary>
/// An item from a browsable file system source (local disk, archive, SFTP, etc.). FullPath is
/// meaningful only within the IFileSystemProvider that produced this item - not a globally
/// unique cross-provider address.
/// </summary>
public interface IFileSystemItem
{
    /// <summary>
    /// Name of the file or directory (without path).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Path to pass back into the same provider's ListChildrenAsync/OpenReadAsync to address
    /// this item.
    /// </summary>
    string FullPath { get; }

    /// <summary>
    /// File extension (including the dot, e.g. ".txt"), empty for directories.
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Size in bytes (0 for directories).
    /// </summary>
    long Size { get; }

    DateTime Created { get; }
    DateTime Modified { get; }
    DateTime Accessed { get; }

    FileSystemItemType ItemType { get; }

    bool CanRead { get; }
    bool CanWrite { get; }
    bool CanExecute { get; }
    bool IsHidden { get; }

    /// <summary>
    /// For symbolic links, the target path.
    /// </summary>
    string? LinkTarget { get; }

    /// <summary>
    /// Human-readable size string (e.g. "1.5 MB", "&lt;DIR&gt;").
    /// </summary>
    string DisplaySize { get; }
}

public enum FileSystemItemType
{
    File,
    Directory,
    SymbolicLink,
}
