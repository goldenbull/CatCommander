using CatCommander.Models;

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
