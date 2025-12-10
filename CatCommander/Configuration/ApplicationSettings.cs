namespace CatCommander.Configuration;

/// <summary>
/// Global application settings loaded from app.toml
/// </summary>
public class ApplicationSettings
{
    public string Title { get; set; } = "CatCommander";
    public int Window_Width { get; set; } = 1200;
    public int Window_Height { get; set; } = 800;
    public string Theme { get; set; } = "dark";
    public int Font_Size { get; set; } = 12;
    public bool Show_Hidden { get; set; } = false; // show hidden files and folders
    public bool Confirm_Delete { get; set; } = true;
    public bool Confirm_Overwrite { get; set; } = true;
    public bool Follow_Symlinks { get; set; } = true;
}