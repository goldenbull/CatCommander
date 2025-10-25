using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using NLog;

namespace CatCommander.Utils;

/// <summary>
/// Provides filesystem information across different operating systems
/// including drives, mounted devices, and mapped network shares
/// </summary>
public static class FileSystemHelper
{
    private static readonly Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Gets all available drives on the system
    /// </summary>
    public static IEnumerable<DriveInformation> GetAllDrives()
    {
        try
        {
            var drives = DriveInfo.GetDrives();
            return drives.Select(d => CreateDriveInformation(d));
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to get drives");
            return [];
        }
    }

    /// <summary>
    /// Gets only ready/available drives (excludes drives that are not ready)
    /// </summary>
    public static IEnumerable<DriveInformation> GetReadyDrives()
    {
        return GetAllDrives().Where(d => d.IsReady);
    }

    /// <summary>
    /// Gets all local fixed drives (internal hard drives, SSDs)
    /// </summary>
    public static IEnumerable<DriveInformation> GetLocalDrives()
    {
        return GetAllDrives().Where(d => d.Type == DriveType.Fixed);
    }

    /// <summary>
    /// Gets all removable drives (USB drives, SD cards, etc.)
    /// </summary>
    public static IEnumerable<DriveInformation> GetRemovableDrives()
    {
        return GetAllDrives().Where(d => d.Type == DriveType.Removable);
    }

    /// <summary>
    /// Gets all network drives (mapped network shares)
    /// </summary>
    public static IEnumerable<DriveInformation> GetNetworkDrives()
    {
        return GetAllDrives().Where(d => d.Type == DriveType.Network);
    }

    /// <summary>
    /// Gets all CD-ROM/DVD drives
    /// </summary>
    public static IEnumerable<DriveInformation> GetOpticalDrives()
    {
        return GetAllDrives().Where(d => d.Type == DriveType.CDRom);
    }

    /// <summary>
    /// Gets the current operating system
    /// </summary>
    public static OperatingSystemType GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OperatingSystemType.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OperatingSystemType.MacOS;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OperatingSystemType.Linux;
        return OperatingSystemType.Unknown;
    }

    /// <summary>
    /// Gets special folders/locations for the current OS
    /// </summary>
    public static IEnumerable<SpecialLocation> GetSpecialLocations()
    {
        var locations = new List<SpecialLocation>();

        try
        {
            // Common locations across all platforms
            AddSpecialLocation(locations, "Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            AddSpecialLocation(locations, "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
            AddSpecialLocation(locations, "Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            AddSpecialLocation(locations, "Downloads", GetDownloadsFolder());
            AddSpecialLocation(locations, "Music", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            AddSpecialLocation(locations, "Pictures", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            AddSpecialLocation(locations, "Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));

            var os = GetOperatingSystem();

            // Platform-specific locations
            switch (os)
            {
                case OperatingSystemType.Windows:
                    AddSpecialLocation(locations, "This PC", "");
                    AddSpecialLocation(locations, "Program Files", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
                    AddSpecialLocation(locations, "Windows", Environment.GetFolderPath(Environment.SpecialFolder.Windows));
                    break;

                case OperatingSystemType.MacOS:
                    AddSpecialLocation(locations, "Applications", "/Applications");
                    AddSpecialLocation(locations, "Library", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library"));
                    AddSpecialLocation(locations, "Volumes", "/Volumes");
                    break;

                case OperatingSystemType.Linux:
                    AddSpecialLocation(locations, "Root", "/");
                    AddSpecialLocation(locations, "Home", "/home");
                    AddSpecialLocation(locations, "Media", "/media");
                    AddSpecialLocation(locations, "Mnt", "/mnt");
                    break;
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, "Failed to get special locations");
        }

        return locations;
    }

    /// <summary>
    /// Gets the path separator for the current OS
    /// </summary>
    public static char GetPathSeparator()
    {
        return Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Formats a file size in bytes to human-readable format
    /// </summary>
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

    /// <summary>
    /// Checks if a path is a network path
    /// </summary>
    public static bool IsNetworkPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root))
                return false;

            // UNC paths start with \\
            if (root.StartsWith(@"\\") || root.StartsWith("//"))
                return true;

            var driveInfo = new DriveInfo(root);
            return driveInfo.DriveType == DriveType.Network;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the system icon for a file or folder (as shown in Finder/Explorer)
    /// Returns null if the icon cannot be retrieved
    /// </summary>
    /// <param name="fullPath">Full path to the file or folder</param>
    /// <returns>Icon identifier/path or null</returns>
    public static string? GetSystemIcon(string fullPath)
    {
        if (string.IsNullOrEmpty(fullPath))
            return null;

        try
        {
            var os = GetOperatingSystem();

            switch (os)
            {
                case OperatingSystemType.Windows:
                    return GetWindowsIcon(fullPath);

                case OperatingSystemType.MacOS:
                    return GetMacOSIcon(fullPath);

                case OperatingSystemType.Linux:
                    return GetLinuxIcon(fullPath);

                default:
                    return GetFallbackIcon(fullPath);
            }
        }
        catch (Exception ex)
        {
            log.Error(ex, $"Failed to get system icon for: {fullPath}");
            return null;
        }
    }

    private static string? GetWindowsIcon(string fullPath)
    {
        // On Windows, we return the file path itself
        // The icon can be extracted using SHGetFileInfo in the UI layer
        // For now, return the extension which can be used to determine icon
        try
        {
            if (Directory.Exists(fullPath))
                return "folder";

            var extension = Path.GetExtension(fullPath)?.ToLowerInvariant();
            return string.IsNullOrEmpty(extension) ? "file" : extension;
        }
        catch
        {
            return "file";
        }
    }

    private static string? GetMacOSIcon(string fullPath)
    {
        // On macOS, NSWorkspace can be used to get icons
        // For now, we'll return a type identifier
        try
        {
            if (Directory.Exists(fullPath))
                return "folder";

            var extension = Path.GetExtension(fullPath)?.ToLowerInvariant();

            // Return UTI (Uniform Type Identifier) style identifiers
            return extension switch
            {
                ".txt" => "public.plain-text",
                ".pdf" => "com.adobe.pdf",
                ".jpg" or ".jpeg" => "public.jpeg",
                ".png" => "public.png",
                ".gif" => "com.compuserve.gif",
                ".mp3" => "public.mp3",
                ".mp4" => "public.mpeg-4",
                ".zip" => "public.zip-archive",
                ".dmg" => "com.apple.disk-image",
                ".app" => "com.apple.application-bundle",
                _ => string.IsNullOrEmpty(extension) ? "public.data" : extension
            };
        }
        catch
        {
            return "public.data";
        }
    }

    private static string? GetLinuxIcon(string fullPath)
    {
        // On Linux, icon names follow freedesktop.org standards
        // Return MIME type based icon names
        try
        {
            if (Directory.Exists(fullPath))
                return "folder";

            var extension = Path.GetExtension(fullPath)?.ToLowerInvariant();

            return extension switch
            {
                ".txt" => "text-plain",
                ".pdf" => "application-pdf",
                ".jpg" or ".jpeg" => "image-jpeg",
                ".png" => "image-png",
                ".gif" => "image-gif",
                ".mp3" => "audio-mpeg",
                ".mp4" => "video-mp4",
                ".zip" or ".tar" or ".gz" => "application-zip",
                ".deb" => "application-x-deb",
                ".rpm" => "application-x-rpm",
                ".sh" => "text-x-script",
                _ => "text-x-generic"
            };
        }
        catch
        {
            return "text-x-generic";
        }
    }

    private static string? GetFallbackIcon(string fullPath)
    {
        // Fallback for unknown OS
        try
        {
            if (Directory.Exists(fullPath))
                return "folder";

            var extension = Path.GetExtension(fullPath)?.ToLowerInvariant();
            return string.IsNullOrEmpty(extension) ? "file" : extension;
        }
        catch
        {
            return "file";
        }
    }

    private static DriveInformation CreateDriveInformation(DriveInfo drive)
    {
        var info = new DriveInformation
        {
            Name = drive.Name,
            Type = drive.DriveType,
            IsReady = drive.IsReady
        };

        if (drive.IsReady)
        {
            try
            {
                info.Label = string.IsNullOrEmpty(drive.VolumeLabel) ? drive.Name : drive.VolumeLabel;
                info.FileSystem = drive.DriveFormat;
                info.TotalSize = drive.TotalSize;
                info.AvailableSpace = drive.AvailableFreeSpace;
                info.UsedSpace = drive.TotalSize - drive.AvailableFreeSpace;
            }
            catch (Exception ex)
            {
                log.Debug(ex, $"Failed to get details for drive {drive.Name}");
                info.Label = drive.Name;
            }
        }
        else
        {
            info.Label = drive.Name;
        }

        return info;
    }

    private static void AddSpecialLocation(List<SpecialLocation> locations, string name, string path)
    {
        if (!string.IsNullOrEmpty(path) && (string.IsNullOrEmpty(path) || Directory.Exists(path) || path == ""))
        {
            locations.Add(new SpecialLocation
            {
                Name = name,
                Path = path
            });
        }
    }

    private static string GetDownloadsFolder()
    {
        // Try to get the Downloads folder path
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var downloadsPath = Path.Combine(userProfile, "Downloads");

        if (Directory.Exists(downloadsPath))
            return downloadsPath;

        // Fallback for older systems
        return userProfile;
    }
}

/// <summary>
/// Information about a drive/volume
/// </summary>
public class DriveInformation
{
    /// <summary>
    /// Drive name/path (e.g., "C:\", "/dev/sda1", "/Volumes/USB")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Volume label or drive name
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Type of drive
    /// </summary>
    public DriveType Type { get; set; }

    /// <summary>
    /// File system type (e.g., "NTFS", "FAT32", "ext4", "APFS")
    /// </summary>
    public string FileSystem { get; set; } = string.Empty;

    /// <summary>
    /// Total size in bytes
    /// </summary>
    public long TotalSize { get; set; }

    /// <summary>
    /// Available free space in bytes
    /// </summary>
    public long AvailableSpace { get; set; }

    /// <summary>
    /// Used space in bytes
    /// </summary>
    public long UsedSpace { get; set; }

    /// <summary>
    /// Whether the drive is ready/available
    /// </summary>
    public bool IsReady { get; set; }

    /// <summary>
    /// Formatted total size string
    /// </summary>
    public string FormattedTotalSize => FileSystemHelper.FormatFileSize(TotalSize);

    /// <summary>
    /// Formatted available space string
    /// </summary>
    public string FormattedAvailableSpace => FileSystemHelper.FormatFileSize(AvailableSpace);

    /// <summary>
    /// Formatted used space string
    /// </summary>
    public string FormattedUsedSpace => FileSystemHelper.FormatFileSize(UsedSpace);

    /// <summary>
    /// Usage percentage (0-100)
    /// </summary>
    public double UsagePercentage => TotalSize > 0 ? (UsedSpace * 100.0 / TotalSize) : 0;

    /// <summary>
    /// Display name for UI
    /// </summary>
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(Label) || Label == Name)
                return $"{Name} ({GetTypeDescription()})";
            return $"{Label} ({Name})";
        }
    }

    /// <summary>
    /// Icon identifier for UI
    /// </summary>
    public string Icon
    {
        get
        {
            return Type switch
            {
                DriveType.Fixed => "💾",
                DriveType.Removable => "🔌",
                DriveType.Network => "🌐",
                DriveType.CDRom => "📀",
                DriveType.Ram => "⚡",
                _ => "📁"
            };
        }
    }

    private string GetTypeDescription()
    {
        return Type switch
        {
            DriveType.Fixed => "Local Disk",
            DriveType.Removable => "Removable Drive",
            DriveType.Network => "Network Drive",
            DriveType.CDRom => "CD/DVD Drive",
            DriveType.Ram => "RAM Disk",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Special system locations (e.g., Desktop, Documents, etc.)
/// </summary>
public class SpecialLocation
{
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full path to the location
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Icon identifier for UI
    /// </summary>
    public string Icon
    {
        get
        {
            return Name.ToLowerInvariant() switch
            {
                "home" => "🏠",
                "desktop" => "🖥️",
                "documents" => "📄",
                "downloads" => "⬇️",
                "music" => "🎵",
                "pictures" => "🖼️",
                "videos" => "🎬",
                "applications" => "📦",
                "library" => "📚",
                "volumes" => "💿",
                "this pc" => "💻",
                _ => "📁"
            };
        }
    }
}

/// <summary>
/// Operating system types
/// </summary>
public enum OperatingSystemType
{
    Unknown,
    Windows,
    MacOS,
    Linux
}
