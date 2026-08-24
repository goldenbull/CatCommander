namespace CatCommander.QuickAccess;

/// <summary>
/// A category for a quick access entry, driving icon selection - kept as data only (no Bitmap
/// property) so this stays in libcat without an Avalonia dependency; the UI layer maps this to
/// an actual icon resource.
/// </summary>
public enum QuickAccessKind
{
    Drive,
    Removable,
    Network,
    Optical,
    SpecialFolder,
}

public class QuickAccessEntry
{
    public required string DisplayName { get; init; }
    public required string Path { get; init; }
    public required QuickAccessKind Kind { get; init; }
}
