using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace CatCommander.Models;

/// <summary>
/// Model representing drive information for UI display
/// </summary>
public class DriveInfoModel
{
    public string Name { get; set; } = string.Empty;
    public string VolumeLabel { get; set; } = string.Empty;
    public DriveType DriveType { get; set; }
    public string RootDirectory { get; set; } = string.Empty;
    public bool IsReady { get; set; }

    public static DriveInfoModel FromDriveInfo(DriveInfo driveInfo)
    {
        return new DriveInfoModel
        {
            Name = driveInfo.Name,
            VolumeLabel = driveInfo.IsReady ? driveInfo.VolumeLabel : driveInfo.Name,
            DriveType = driveInfo.DriveType,
            RootDirectory = driveInfo.RootDirectory.FullName,
            IsReady = driveInfo.IsReady
        };
    }

    /// <summary>
    /// Returns a display name for the drive (volume label or name)
    /// </summary>
    public string DisplayName => Path.GetFileName(string.IsNullOrEmpty(VolumeLabel) ? Name : VolumeLabel);

    /// <summary>
    /// Returns a Bitmap for the drive icon
    /// </summary>
    public Bitmap Icon
    {
        get
        {
            var iconPath = DriveType switch
            {
                DriveType.Fixed => "hard_disk.png",
                DriveType.Removable => "usb_flash_drive.png",
                DriveType.Network => "network.png",
                DriveType.CDRom => "cdrom.png",
                DriveType.Ram => "hard_disk.png",
                _ => "empty.png"
            };
            
            var uri = new Uri($"avares://CatCommander/Images/{iconPath}");
            return new Bitmap(AssetLoader.Open(uri));
        }
    }
}