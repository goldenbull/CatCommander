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
