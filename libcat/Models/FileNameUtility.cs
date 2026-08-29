namespace CatCommander.Models;

public static class FileNameUtility
{
    private const string TarGZipExtension = ".tar.gz";

    public static string GetExtension(string name) =>
        name.EndsWith(TarGZipExtension, StringComparison.OrdinalIgnoreCase)
            ? name[^TarGZipExtension.Length..]
            : Path.GetExtension(name);
}
