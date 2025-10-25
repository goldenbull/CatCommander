using System;
using System.IO;
using System.Linq;
using CatCommander.Utils;

namespace CatCommander.Utils;

/// <summary>
/// Example usage of FileSystemHelper
/// </summary>
public static class FileSystemHelperExample
{
    /// <summary>
    /// Example: Display all drives and their information
    /// </summary>
    public static void Example1_ListAllDrives()
    {
        Console.WriteLine("=== All Drives ===\n");

        var drives = FileSystemHelper.GetAllDrives();
        foreach (var drive in drives)
        {
            Console.WriteLine($"{drive.Icon} {drive.DisplayName}");
            Console.WriteLine($"   Type: {drive.Type}");
            Console.WriteLine($"   Ready: {drive.IsReady}");

            if (drive.IsReady)
            {
                Console.WriteLine($"   File System: {drive.FileSystem}");
                Console.WriteLine($"   Total: {drive.FormattedTotalSize}");
                Console.WriteLine($"   Used: {drive.FormattedUsedSpace} ({drive.UsagePercentage:F1}%)");
                Console.WriteLine($"   Free: {drive.FormattedAvailableSpace}");
            }

            Console.WriteLine();
        }
    }

    /// <summary>
    /// Example: List drives by category
    /// </summary>
    public static void Example2_ListDrivesByCategory()
    {
        Console.WriteLine("=== Local Drives ===");
        foreach (var drive in FileSystemHelper.GetLocalDrives())
        {
            Console.WriteLine($"  {drive.Icon} {drive.DisplayName}");
        }

        Console.WriteLine("\n=== Removable Drives ===");
        foreach (var drive in FileSystemHelper.GetRemovableDrives())
        {
            Console.WriteLine($"  {drive.Icon} {drive.DisplayName}");
        }

        Console.WriteLine("\n=== Network Drives ===");
        foreach (var drive in FileSystemHelper.GetNetworkDrives())
        {
            Console.WriteLine($"  {drive.Icon} {drive.DisplayName}");
        }

        Console.WriteLine("\n=== Optical Drives ===");
        foreach (var drive in FileSystemHelper.GetOpticalDrives())
        {
            Console.WriteLine($"  {drive.Icon} {drive.DisplayName}");
        }
    }

    /// <summary>
    /// Example: Display special locations
    /// </summary>
    public static void Example3_SpecialLocations()
    {
        Console.WriteLine("=== Special Locations ===\n");

        var locations = FileSystemHelper.GetSpecialLocations();
        foreach (var location in locations)
        {
            Console.WriteLine($"{location.Icon} {location.Name}");
            Console.WriteLine($"   Path: {location.Path}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Example: Detect operating system and show platform-specific info
    /// </summary>
    public static void Example4_PlatformInfo()
    {
        var os = FileSystemHelper.GetOperatingSystem();
        var separator = FileSystemHelper.GetPathSeparator();

        Console.WriteLine("=== Platform Information ===\n");
        Console.WriteLine($"Operating System: {os}");
        Console.WriteLine($"Path Separator: '{separator}'");
        Console.WriteLine();

        // Show only ready drives with usage information
        Console.WriteLine("=== Ready Drives ===\n");
        var readyDrives = FileSystemHelper.GetReadyDrives();
        foreach (var drive in readyDrives)
        {
            Console.WriteLine($"{drive.Icon} {drive.Label} - {drive.FormattedUsedSpace} used of {drive.FormattedTotalSize}");

            // Draw a simple usage bar
            var barWidth = 40;
            var usedBars = (int)(drive.UsagePercentage / 100.0 * barWidth);
            var freeBars = barWidth - usedBars;
            Console.WriteLine($"   [{new string('█', usedBars)}{new string('░', freeBars)}] {drive.UsagePercentage:F1}%");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Example: Check if paths are network paths
    /// </summary>
    public static void Example5_NetworkPathDetection()
    {
        Console.WriteLine("=== Network Path Detection ===\n");

        var testPaths = new[]
        {
            @"C:\Users\Documents",
            @"\\server\share\folder",
            @"/home/user/documents",
            @"//192.168.1.100/files"
        };

        foreach (var path in testPaths)
        {
            var isNetwork = FileSystemHelper.IsNetworkPath(path);
            var icon = isNetwork ? "🌐" : "💾";
            Console.WriteLine($"{icon} {path} - {(isNetwork ? "Network" : "Local")}");
        }
    }

    /// <summary>
    /// Example: Format various file sizes
    /// </summary>
    public static void Example6_FileSizeFormatting()
    {
        Console.WriteLine("=== File Size Formatting ===\n");

        var sizes = new long[]
        {
            512,                          // 512 B
            1024,                         // 1 KB
            1048576,                      // 1 MB
            1073741824,                   // 1 GB
            1099511627776,                // 1 TB
            1234567890                    // 1.15 GB
        };

        foreach (var size in sizes)
        {
            Console.WriteLine($"{size,15} bytes = {FileSystemHelper.FormatFileSize(size)}");
        }
    }

    /// <summary>
    /// Example: Get system icons for files
    /// </summary>
    public static void Example6b_GetSystemIcons()
    {
        Console.WriteLine("=== System Icon Identifiers ===\n");

        var testPaths = new[]
        {
            @"/Users/test/Documents",
            @"/Users/test/file.txt",
            @"/Users/test/image.png",
            @"/Users/test/video.mp4",
            @"/Users/test/document.pdf",
            @"/Users/test/archive.zip",
            @"C:\Windows\System32\notepad.exe",
            @"C:\Users\test\document.docx"
        };

        foreach (var path in testPaths)
        {
            var iconId = FileSystemHelper.GetSystemIcon(path);
            var fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                fileName = path;

            Console.WriteLine($"{fileName,-30} → Icon: {iconId ?? "(none)"}");
        }
    }

    /// <summary>
    /// Example: Build a file manager navigation tree
    /// </summary>
    public static void Example7_FileManagerNavigation()
    {
        Console.WriteLine("=== File Manager Navigation Tree ===\n");

        // Top level: Special locations
        Console.WriteLine("📌 Quick Access");
        foreach (var location in FileSystemHelper.GetSpecialLocations())
        {
            Console.WriteLine($"  {location.Icon} {location.Name}");
        }

        // Second level: Drives grouped by type
        Console.WriteLine("\n💻 This PC");

        var localDrives = FileSystemHelper.GetLocalDrives();
        if (localDrives.Any())
        {
            Console.WriteLine("  💾 Local Disks");
            foreach (var drive in localDrives)
            {
                Console.WriteLine($"    {drive.DisplayName}");
            }
        }

        var removableDrives = FileSystemHelper.GetRemovableDrives();
        if (removableDrives.Any())
        {
            Console.WriteLine("  💿 Removable Devices");
            foreach (var drive in removableDrives)
            {
                Console.WriteLine($"    {drive.DisplayName}");
            }
        }

        var networkDrives = FileSystemHelper.GetNetworkDrives();
        if (networkDrives.Any())
        {
            Console.WriteLine("  🌐 Network Locations");
            foreach (var drive in networkDrives)
            {
                Console.WriteLine($"    {drive.DisplayName}");
            }
        }
    }
}
