using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatCommander.Models;
using NLog;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace CatCommander.Utils;

/// <summary>
/// Provides archive file operations including loading file information from zip archives
/// and building hierarchical tree structures
/// </summary>
public static class ArchiveFileHelper
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Loads file information from a zip archive and builds a FileItemTreeNode
    /// </summary>
    /// <param name="archiveFullPath">Full path to the archive file</param>
    /// <returns>Root FileItemTreeNode containing the archive structure, or null on error</returns>
    public static FileItemTreeNode? LoadArchive(string archiveFullPath)
    {
        if (string.IsNullOrEmpty(archiveFullPath))
        {
            log.Warn("Archive path is null or empty");
            return null;
        }

        if (!File.Exists(archiveFullPath))
        {
            log.Warn($"Archive file does not exist: {archiveFullPath}");
            return null;
        }

        try
        {
            var items = ExtractArchiveEntries(archiveFullPath);
            if (!items.Any())
            {
                log.Warn($"No entries found in archive: {archiveFullPath}");
                return null;
            }

            log.Debug($"Loaded {items.Count} entries from archive: {archiveFullPath}");
            return FileItemTreeNode.CreateFrom(items, '/');
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to load archive: {archiveFullPath}");
            return null;
        }
    }

    /// <summary>
    /// Extracts archive entries as a flat list of IFileSystemItem objects
    /// </summary>
    /// <param name="archiveFullPath">Full path to the archive file</param>
    /// <returns>List of file system items from the archive</returns>
    public static List<IFileSystemItem> ExtractArchiveEntries(string archiveFullPath)
    {
        var items = new List<IFileSystemItem>();

        if (!File.Exists(archiveFullPath))
        {
            return items;
        }

        try
        {
            using var archive = ZipArchive.Open(archiveFullPath);

            foreach (var entry in archive.Entries)
            {
                // Skip if entry is null
                if (entry == null)
                    continue;

                var item = ConvertEntryToFileItem(entry);
                if (item != null)
                {
                    items.Add(item);
                }
            }

            log.Debug($"Extracted {items.Count} entries from archive: {archiveFullPath}");
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to extract archive entries from: {archiveFullPath}");
        }

        return items;
    }

    /// <summary>
    /// Checks if a file is a supported archive type
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <returns>True if the file is a supported archive</returns>
    public static bool IsArchiveFile(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

        return extension switch
        {
            ".zip" => true,
            ".7z" => true,
            ".rar" => true,
            ".tar" => true,
            ".gz" => true,
            ".bz2" => true,
            ".xz" => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets archive information including total size and entry count
    /// </summary>
    /// <param name="archiveFullPath">Full path to the archive file</param>
    /// <returns>Archive information or null on error</returns>
    public static ArchiveInformation? GetArchiveInfo(string archiveFullPath)
    {
        if (!File.Exists(archiveFullPath))
            return null;

        try
        {
            using var archive = ZipArchive.Open(archiveFullPath);

            var entries = archive.Entries.Where(e => e != null).ToList();
            var totalSize = entries.Sum(e => (long)e.Size);
            var compressedSize = entries.Sum(e => (long)e.CompressedSize);
            var fileCount = entries.Count(e => !e.IsDirectory);
            var directoryCount = entries.Count(e => e.IsDirectory);

            return new ArchiveInformation
            {
                ArchivePath = archiveFullPath,
                TotalEntries = entries.Count(),
                FileCount = fileCount,
                DirectoryCount = directoryCount,
                TotalUncompressedSize = totalSize,
                TotalCompressedSize = compressedSize,
                CompressionRatio = totalSize > 0 ? (1.0 - (double)compressedSize / totalSize) * 100 : 0
            };
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get archive info for: {archiveFullPath}");
            return null;
        }
    }

    private static FileItemModel? ConvertEntryToFileItem(IArchiveEntry entry)
    {
        try
        {
            // Skip if entry has no key
            if (string.IsNullOrEmpty(entry.Key))
                return null;

            // Normalize path separators to forward slash
            var path = entry.Key.Replace('\\', '/');

            // Skip empty paths
            if (string.IsNullOrEmpty(path))
                return null;

            // Determine name (last component of path)
            var name = path.TrimEnd('/');
            var lastSlash = name.LastIndexOf('/');
            if (lastSlash >= 0)
            {
                name = name.Substring(lastSlash + 1);
            }

            // Skip if no name (root directory entries)
            if (string.IsNullOrEmpty(name))
                return null;

            var item = new FileItemModel
            {
                Name = name,
                FullPath = path.TrimEnd('/'),
                Size = entry.IsDirectory ? 0 : (long)entry.Size,
                Modified = entry.LastModifiedTime ?? DateTime.MinValue,
                Created = entry.CreatedTime ?? DateTime.MinValue,
                Accessed = entry.LastAccessedTime ?? DateTime.MinValue,
                Extension = entry.IsDirectory ? string.Empty : Path.GetExtension(name),
                ItemType = entry.IsDirectory ? FileSystemItemType.Directory : FileSystemItemType.File,
                CanRead = true,
                CanWrite = false, // Archive entries are read-only
                CanExecute = false,
                IsHidden = false
            };

            return item;
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to convert archive entry to file item: {entry.Key}");
            return null;
        }
    }
}

/// <summary>
/// Information about an archive file
/// </summary>
public class ArchiveInformation
{
    /// <summary>
    /// Full path to the archive file
    /// </summary>
    public string ArchivePath { get; set; } = string.Empty;

    /// <summary>
    /// Total number of entries in the archive
    /// </summary>
    public int TotalEntries { get; set; }

    /// <summary>
    /// Number of files (non-directories)
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Number of directories
    /// </summary>
    public int DirectoryCount { get; set; }

    /// <summary>
    /// Total uncompressed size in bytes
    /// </summary>
    public long TotalUncompressedSize { get; set; }

    /// <summary>
    /// Total compressed size in bytes
    /// </summary>
    public long TotalCompressedSize { get; set; }

    /// <summary>
    /// Compression ratio as percentage (0-100)
    /// </summary>
    public double CompressionRatio { get; set; }

    /// <summary>
    /// Formatted total uncompressed size
    /// </summary>
    public string FormattedUncompressedSize => FileSystemHelper.FormatFileSize(TotalUncompressedSize);

    /// <summary>
    /// Formatted total compressed size
    /// </summary>
    public string FormattedCompressedSize => FileSystemHelper.FormatFileSize(TotalCompressedSize);

    /// <summary>
    /// Display summary
    /// </summary>
    public string Summary => $"{TotalEntries} entries ({FileCount} files, {DirectoryCount} folders) - " +
                            $"{FormattedUncompressedSize} → {FormattedCompressedSize} " +
                            $"({CompressionRatio:F1}% compression)";
}
