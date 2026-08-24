namespace CatCommander.Config;

/// <summary>
/// Panel state settings loaded from panels.toml
/// Stores navigation state for each panel (path, sort order, etc.)
/// </summary>
public class PanelSettings
{
    public string Root_Path { get; set; } = "";
    public bool Locked { get; set; } = false;
    public string Sort_Column { get; set; } = "name";
    public bool Sort_Ascending { get; set; } = true;
}