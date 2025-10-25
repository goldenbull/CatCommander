using System;

namespace CatCommander.Models;

public class FileItemModel : IFileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Created { get; set; } = DateTime.MinValue;
    public DateTime Modified { get; set; } = DateTime.MinValue;
    public DateTime Accessed { get; set; } = DateTime.MinValue;
    public string Extension { get; set; } = string.Empty;
    public FileSystemItemType ItemType { get; set; } = FileSystemItemType.File;
    public bool CanRead { get; set; } = true;
    public bool CanWrite { get; set; } = true;
    public bool CanExecute { get; set; } = false;
    public bool IsHidden { get; set; } = false;
    public string? LinkTarget { get; set; }

    /// <summary>
    /// Backward compatibility property for IsDirectory
    /// </summary>
    public bool IsDirectory
    {
        get => ItemType == FileSystemItemType.Directory || ItemType == FileSystemItemType.Special;
        set => ItemType = value ? FileSystemItemType.Directory : FileSystemItemType.File;
    }

    public string DisplaySize
    {
        get
        {
            return ItemType switch
            {
                FileSystemItemType.Directory => "<DIR>",
                FileSystemItemType.Special => "<DIR>",
                FileSystemItemType.SymbolicLink => "<LINK>",
                _ => FormatFileSize(Size)
            };
        }
    }

    public string DisplayIcon
    {
        get
        {
            return ItemType switch
            {
                FileSystemItemType.Directory => "📁",
                FileSystemItemType.SymbolicLink => "🔗",
                FileSystemItemType.Special => "⬆️",
                _ => GetFileIcon()
            };
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:N2} {suffixes[suffixIndex]}";
    }

    private string GetFileIcon()
    {
        // Simple file type icon based on extension
        return Extension.ToLowerInvariant() switch
        {
            ".txt" or ".md" or ".log" => "📄",
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "🖼️",
            ".mp3" or ".wav" or ".flac" or ".ogg" => "🎵",
            ".mp4" or ".avi" or ".mkv" or ".mov" => "🎬",
            ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
            ".exe" or ".dll" or ".so" or ".dylib" => "⚙️",
            ".cs" or ".java" or ".py" or ".js" or ".ts" => "💻",
            _ => "📃"
        };
    }
}
