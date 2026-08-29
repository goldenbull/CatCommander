using CatCommander.Models;
using CatCommander.Resources;

namespace CatCommander.FileSystem;

/// <summary>
/// Browses one file system source (local disk, archive, SFTP session, ...). Async throughout,
/// even though LocalFileSystemProvider's implementation is local I/O wrapped in Task.Run - remote
/// providers (SFTP, network shares) genuinely need this to not be synchronous, and getting the
/// interface right now means the ViewModel layer never has to change when they're added.
/// </summary>
public interface IFileSystemProvider
{
    /// <summary>
    /// Stable provider kind/session identifier used by ResourceRef diagnostics and persistence.
    /// Existing providers get a compatible type-based default; connection-based providers should
    /// override it with a session-aware id.
    /// </summary>
    string Id => GetType().FullName ?? GetType().Name;

    /// <summary>
    /// Provider-specific namespace semantics. Remote Unix filesystems and archive entry names are
    /// case-sensitive even when CatCommander itself is running on Windows or macOS.
    /// </summary>
    StringComparer PathComparer => StringComparer.Ordinal;
    StringComparer NameComparer => StringComparer.Ordinal;

    /// <summary>
    /// Whether the current synchronous TreeDataGrid child selector may enumerate this provider.
    /// Network providers must leave this false until the UI has a genuinely asynchronous tree
    /// adapter; ordinary list navigation and flattened expansion remain fully asynchronous.
    /// </summary>
    bool SupportsTreeMode => false;

    bool IsSameFileSystem(IFileSystemProvider other) =>
        string.Equals(Id, other.Id, StringComparison.Ordinal);

    /// <summary>Provider-wide defaults; individual resources may further restrict them later.</summary>
    ResourceCapabilities ResourceCapabilities =>
        CatCommander.Resources.ResourceCapabilities.Read |
        CatCommander.Resources.ResourceCapabilities.EnumerateChildren |
        (this is IResourceMutationProvider
            ? CatCommander.Resources.ResourceCapabilities.Rename | CatCommander.Resources.ResourceCapabilities.Delete
            : CatCommander.Resources.ResourceCapabilities.None) |
        (this is IExternalOpenProvider
            ? CatCommander.Resources.ResourceCapabilities.OpenExternally
            : CatCommander.Resources.ResourceCapabilities.None);

    ContainerCapabilities ContainerCapabilities =>
        (this is IWritableResourceProvider
            ? CatCommander.Resources.ContainerCapabilities.AcceptFiles | CatCommander.Resources.ContainerCapabilities.AcceptDirectories
            : CatCommander.Resources.ContainerCapabilities.None) |
        (this is IResourceMutationProvider
            ? CatCommander.Resources.ContainerCapabilities.CreateDirectory
            : CatCommander.Resources.ContainerCapabilities.None);

    /// <summary>
    /// Computes permissions for one item in its containing directory. The default preserves local
    /// behavior, while Unix/SFTP providers can correctly derive Rename/Delete from parent-directory
    /// permissions instead of conflating them with whether the file contents are writable.
    /// </summary>
    ResourceCapabilities GetResourceCapabilities(IFileSystemItem item, ResourceRef? container)
    {
        var capabilities = ResourceCapabilities;
        if (!CanEnter(item))
            capabilities &= ~CatCommander.Resources.ResourceCapabilities.EnumerateChildren;
        if (!item.CanRead)
            capabilities &= ~CatCommander.Resources.ResourceCapabilities.Read;
        if (!item.CanWrite)
        {
            capabilities &= ~(CatCommander.Resources.ResourceCapabilities.Rename |
                              CatCommander.Resources.ResourceCapabilities.Delete);
        }
        return capabilities;
    }

    /// <summary>
    /// Returns the parent address inside this provider's namespace. Archive/SFTP providers can
    /// override path syntax without leaking it into BrowserContext.
    /// </summary>
    string? GetParentPath(string path)
    {
        var child = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetDirectoryName(child);
    }

    ResourceRef? GetParentResource(ResourceRef location)
    {
        var parent = GetParentPath(location.Path);
        return string.IsNullOrEmpty(parent) ? null : new ResourceRef(this, parent);
    }

    /// <summary>
    /// Resource that should become current after leaving <paramref name="location"/> for its
    /// parent. Usually this is the directory just left. Providers whose root is backed by a
    /// resource in another provider (archives/ISO images) return that backing resource instead.
    /// </summary>
    ResourceRef GetParentSelectionResource(ResourceRef location) => location;

    /// <summary>
    /// Lists the immediate children of <paramref name="path"/> (no ".." synthetic entry - that's
    /// a navigation affordance the ViewModel layer adds for list-mode display, not a real child).
    /// </summary>
    Task<IReadOnlyList<IFileSystemItem>> ListChildrenAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Opens a readable stream for a file item's contents.
    /// </summary>
    Task<Stream> OpenReadAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Whether this item is itself another browsable root within this same provider (e.g. a
    /// directory). Archive providers will later say "no" for an item that's actually a nested
    /// archive - entering that needs a different provider, resolved via FileSystemProviderRegistry.
    /// </summary>
    bool CanEnter(IFileSystemItem item);

    /// <summary>
    /// Whether navigating through this provider should be recorded in the address bar's
    /// navigation-history dropdown. True for real, independently-typeable roots (the local file
    /// system, an SFTP session, ...). A future archive provider's path-inside-an-archive
    /// navigation, or the in-place tree expansion Ctrl+B does, should say false: those aren't
    /// places a user would re-type or expect to jump back to from a *path* history - they're
    /// scoped to whatever specific archive/tree is currently open, not general destinations.
    /// </summary>
    bool TracksHistory { get; }
}

public interface IResourceMutationProvider
{
    Task<string> CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default);
    Task<string> RenameAsync(string path, string newName, CancellationToken ct = default);
    Task DeleteAsync(string path, CancellationToken ct = default);
}

/// <summary>Optional optimized same-filesystem transfer operations.</summary>
public interface INativeResourceTransferProvider
{
    Task CopyAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default);
    Task MoveAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default);
}

public interface IExternalOpenProvider
{
    Task OpenExternallyAsync(string path, CancellationToken ct = default);
}

/// <summary>Maps provider resources to real OS paths suitable for native clipboard file items.</summary>
public interface IClipboardFileProvider
{
    string? GetClipboardFilePath(ResourceRef resource);
}
