using Tomlyn.Serialization;

namespace CatCommander.Config;

public sealed class SessionState
{
    [TomlPropertyName("active_panel")]
    public string ActivePanel { get; set; } = "left";

    [TomlPropertyName("left")]
    public PanelSessionState Left { get; set; } = new();

    [TomlPropertyName("right")]
    public PanelSessionState Right { get; set; } = new();
}

public sealed class PanelSessionState
{
    [TomlPropertyName("tabs")]
    public List<string> Tabs { get; set; } = new();

    [TomlPropertyName("active_tab")]
    public int ActiveTab { get; set; }
}
