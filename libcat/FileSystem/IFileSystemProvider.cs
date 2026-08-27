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

    /// <summary>Provider-wide defaults; individual resources may further restrict them later.</summary>
    ResourceCapabilities ResourceCapabilities =>
        CatCommander.Resources.ResourceCapabilities.Read |
        CatCommander.Resources.ResourceCapabilities.EnumerateChildren |
        CatCommander.Resources.ResourceCapabilities.Rename |
        CatCommander.Resources.ResourceCapabilities.Delete |
        CatCommander.Resources.ResourceCapabilities.OpenExternally;

    ContainerCapabilities ContainerCapabilities =>
        CatCommander.Resources.ContainerCapabilities.AcceptFiles |
        CatCommander.Resources.ContainerCapabilities.AcceptDirectories |
        CatCommander.Resources.ContainerCapabilities.CreateDirectory;

    /// <summary>
    /// Returns the parent address inside this provider's namespace. Archive/SFTP providers can
    /// override path syntax without leaking it into BrowserContext.
    /// </summary>
    string? GetParentPath(string path)
    {
        var child = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetDirectoryName(child);
    }

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
    /// Creates a new, empty subdirectory named <paramref name="name"/> directly under
    /// <paramref name="parentPath"/> (F7 - see ItemBrowserViewModel.CreateDirectoryAsync), returning
    /// its full path.
    /// </summary>
    Task<string> CreateDirectoryAsync(string parentPath, string name, CancellationToken ct = default);

    /// <summary>
    /// Renames an item in place - same parent, only the leaf name changes (F2's in-place edit in
    /// the grid, not a full move to a different directory) - returning its new full path.
    /// </summary>
    Task<string> RenameAsync(string path, string newName, CancellationToken ct = default);

    /// <summary>
    /// Opens this item with the OS's own default handler for its type - Finder/Explorer's own
    /// double-click behavior. Only meaningful for an item CanEnter says no to; entering a directory
    /// goes through NavigateToAsync/ListChildrenAsync instead, never this.
    /// </summary>
    Task OpenExternallyAsync(string path, CancellationToken ct = default);

    /// <summary>
    /// Copies <paramref name="sourcePath"/> (a file, or a directory copied recursively) into
    /// <paramref name="destinationDirectory"/>, keeping its own leaf name - F5's Copy, always run
    /// off the UI thread by FileOperationQueue rather than called directly from a ViewModel.
    /// A name collision at the destination is overwritten, not skipped or prompted for - jobs run
    /// unattended in the background queue, where there's no one to prompt.
    /// <paramref name="progress"/>, if given, is reported once per file actually written (its full
    /// destination path) - for a directory this fires once per descendant file, not once overall.
    /// </summary>
    Task CopyAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default);

    /// <summary>
    /// Moves <paramref name="sourcePath"/> into <paramref name="destinationDirectory"/>, keeping
    /// its own leaf name - F6's Move. Same overwrite-on-collision and progress-reporting contract
    /// as CopyAsync.
    /// </summary>
    Task MoveAsync(string sourcePath, string destinationDirectory, IProgress<string>? progress, CancellationToken ct = default);

    /// <summary>
    /// Deletes <paramref name="path"/> - a file, or a directory and everything in it - Del/F8's
    /// Delete, always run off the UI thread by FileOperationQueue rather than called directly from
    /// a ViewModel. No per-file progress reporting, unlike CopyAsync/MoveAsync: a local recursive
    /// delete has no meaningful per-file latency to report on the way through.
    /// </summary>
    Task DeleteAsync(string path, CancellationToken ct = default);

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
