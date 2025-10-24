using System;

namespace CatCommander.Models;

/// <summary>
/// Interface for file system items from various sources (disk, zip archive, SFTP, FTP, etc.)
/// </summary>
public interface IFileSystemItem
{
    /// <summary>
    /// Name of the file or directory (without path)
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Full path of the item (relative to the data source)
    /// </summary>
    string FullPath { get; }

    /// <summary>
    /// File extension (including the dot, e.g., ".txt"), empty for directories
    /// </summary>
    string Extension { get; }

    /// <summary>
    /// Size in bytes (0 for directories)
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Creation time
    /// </summary>
    DateTime Created { get; }

    /// <summary>
    /// Last modification time
    /// </summary>
    DateTime Modified { get; }

    /// <summary>
    /// Last access time
    /// </summary>
    DateTime Accessed { get; }

    /// <summary>
    /// Type of the item
    /// </summary>
    FileSystemItemType ItemType { get; }

    /// <summary>
    /// Read permission
    /// </summary>
    bool CanRead { get; }

    /// <summary>
    /// Write permission
    /// </summary>
    bool CanWrite { get; }

    /// <summary>
    /// Execute permission
    /// </summary>
    bool CanExecute { get; }

    /// <summary>
    /// Whether the item is hidden
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// For symbolic links, the target path
    /// </summary>
    string? LinkTarget { get; }

    /// <summary>
    /// Human-readable size string (e.g., "1.5 MB", "<DIR>")
    /// </summary>
    string DisplaySize { get; }

    /// <summary>
    /// Icon or identifier for UI display
    /// </summary>
    string DisplayIcon { get; }
}

/// <summary>
/// Type of file system item
/// </summary>
public enum FileSystemItemType
{
    /// <summary>
    /// Regular file
    /// </summary>
    File,

    /// <summary>
    /// Directory/Folder
    /// </summary>
    Directory,

    /// <summary>
    /// Symbolic link or shortcut
    /// </summary>
    SymbolicLink,

    /// <summary>
    /// Special item (e.g., parent directory "..")
    /// </summary>
    Special
}
