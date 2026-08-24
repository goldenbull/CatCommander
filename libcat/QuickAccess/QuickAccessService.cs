namespace CatCommander.QuickAccess;

/// <summary>
/// Platform-appropriate "starting points" list, shown as the drive-list row above each panel.
/// Windows gets a Total Commander-style full drive listing; macOS/Linux get a curated mix of
/// common folders and mounted volumes instead - see per-platform notes below.
/// </summary>
public static class QuickAccessService
{
    public static IReadOnlyList<QuickAccessEntry> GetEntries()
    {
        var entries = new List<QuickAccessEntry>();

        if (OperatingSystem.IsWindows())
        {
            AddWindowsDrives(entries);
        }
        else
        {
            AddCommonFolders(entries);

            if (OperatingSystem.IsMacOS())
                AddMacOSVolumes(entries);
            else if (OperatingSystem.IsLinux())
                AddLinuxMounts(entries);
        }

        return entries;
    }

    private static void AddWindowsDrives(List<QuickAccessEntry> entries)
    {
        foreach (var drive in SafeGetDrives())
        {
            var letter = drive.Name.TrimEnd('\\');
            var displayName = drive.IsReady && !string.IsNullOrEmpty(drive.VolumeLabel)
                ? $"{drive.VolumeLabel} ({letter})"
                : letter;

            entries.Add(new QuickAccessEntry
            {
                DisplayName = displayName,
                Path = drive.RootDirectory.FullName,
                Kind = drive.DriveType switch
                {
                    DriveType.Removable => QuickAccessKind.Removable,
                    DriveType.Network => QuickAccessKind.Network,
                    DriveType.CDRom => QuickAccessKind.Optical,
                    _ => QuickAccessKind.Drive,
                },
            });
        }
    }

    private static void AddCommonFolders(List<QuickAccessEntry> entries)
    {
        AddSpecialFolder(entries, "Home", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        AddSpecialFolder(entries, "Desktop", Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddSpecialFolder(entries, "Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddSpecialFolder(
            entries,
            "Downloads",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"));
    }

    private static void AddMacOSVolumes(List<QuickAccessEntry> entries)
    {
        // Only the boot volume and whatever's mounted under /Volumes - DriveInfo.GetDrives() on
        // macOS also returns a long tail of pseudo-filesystems (devfs, /dev/fd, ...) not useful here.
        foreach (var drive in SafeGetDrives())
        {
            if (drive.Name != "/" && !drive.Name.StartsWith("/Volumes/", StringComparison.Ordinal))
                continue;

            entries.Add(new QuickAccessEntry
            {
                DisplayName = drive.Name == "/" ? "Macintosh HD" : Path.GetFileName(drive.Name.TrimEnd('/')),
                Path = drive.RootDirectory.FullName,
                Kind = QuickAccessKind.Drive,
            });
        }
    }

    private static void AddLinuxMounts(List<QuickAccessEntry> entries)
    {
        AddSpecialFolder(entries, "Root", "/");

        // Not exhaustive - just what's visibly mounted under the conventional removable-media
        // directories, not every entry in /proc/mounts.
        foreach (var mountRoot in new[] { "/media", "/mnt" })
        {
            if (!Directory.Exists(mountRoot))
                continue;

            foreach (var mount in SafeEnumerateDirectories(mountRoot))
            {
                entries.Add(new QuickAccessEntry
                {
                    DisplayName = Path.GetFileName(mount),
                    Path = mount,
                    Kind = QuickAccessKind.Removable,
                });
            }
        }
    }

    private static void AddSpecialFolder(List<QuickAccessEntry> entries, string name, string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            entries.Add(new QuickAccessEntry { DisplayName = name, Path = path, Kind = QuickAccessKind.SpecialFolder });
    }

    private static IEnumerable<DriveInfo> SafeGetDrives()
    {
        try
        {
            return DriveInfo.GetDrives();
        }
        catch (Exception)
        {
            return Enumerable.Empty<DriveInfo>();
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch (Exception)
        {
            return Enumerable.Empty<string>();
        }
    }
}
