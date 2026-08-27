namespace CatCommander.Platform;

public enum PlatformKind
{
    Windows,
    MacOS,
    Linux,
    Other,
}

/// <summary>
/// One application-wide description of the host OS. Platform-specific services consume this
/// value instead of independently detecting and naming operating systems.
/// </summary>
public sealed class PlatformInfo
{
    public static PlatformInfo Current { get; } = new(Detect());

    public PlatformKind Kind { get; }
    public bool IsWindows => Kind == PlatformKind.Windows;
    public bool IsMacOS => Kind == PlatformKind.MacOS;
    public bool IsLinux => Kind == PlatformKind.Linux;

    public PlatformInfo(PlatformKind kind) => Kind = kind;

    private static PlatformKind Detect()
    {
        if (OperatingSystem.IsWindows()) return PlatformKind.Windows;
        if (OperatingSystem.IsMacOS()) return PlatformKind.MacOS;
        if (OperatingSystem.IsLinux()) return PlatformKind.Linux;
        return PlatformKind.Other;
    }
}
