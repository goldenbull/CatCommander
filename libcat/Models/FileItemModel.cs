namespace CatCommander.Models;

public class FileItemModel : IFileSystemItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime Created { get; set; } = DateTime.MinValue;
    public DateTime Modified { get; set; } = DateTime.MinValue;
    public DateTime Accessed { get; set; } = DateTime.MinValue;
    public FileSystemItemType ItemType { get; set; } = FileSystemItemType.File;
    public bool CanRead { get; set; } = true;
    public bool CanWrite { get; set; } = true;
    public bool CanExecute { get; set; }
    public bool IsHidden { get; set; }
    public string? LinkTarget { get; set; }

    public string DisplaySize => ItemType switch
    {
        FileSystemItemType.Directory => "<DIR>",
        FileSystemItemType.SymbolicLink => "<LINK>",
        _ => FormatFileSize(Size),
    };

    public static string FormatFileSize(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB", "PB" };
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:N2} {suffixes[suffixIndex]}";
    }
}
