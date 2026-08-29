using Tomlyn.Serialization;

namespace CatCommander.Config;

/// <summary>Root of config.toml. New settings belong here instead of acquiring their own files.</summary>
public sealed class ApplicationSettings
{
    [TomlPropertyName("shortcuts")]
    public ShortcutsSettings Shortcuts { get; set; } = new();

    [TomlPropertyName("terminal")]
    public TerminalSettings Terminal { get; set; } = new();

    [TomlPropertyName("favorites")]
    public FavoritesSettings Favorites { get; set; } = new();
}

public sealed class FavoritesSettings
{
    [TomlPropertyName("paths")]
    public List<string> Paths { get; set; } = new();
}

public sealed class TerminalSettings
{
    /// <summary>Windows terminal host: "cmd" (default) or "powershell".</summary>
    [TomlPropertyName("windows_shell")]
    public string WindowsShell { get; set; } = "cmd";
}
