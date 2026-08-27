using CatCommander.Resources;

namespace CatCommander.FileSystem;

/// <summary>Optional capability for providers backed by a local filesystem location.</summary>
public interface ILocalShellContextProvider
{
    /// <summary>
    /// Returns the directory where a local terminal belongs. An archive provider should return
    /// the directory containing its archive file, not a virtual path inside the archive.
    /// </summary>
    string? GetLocalShellDirectory(ResourceRef location);
}
