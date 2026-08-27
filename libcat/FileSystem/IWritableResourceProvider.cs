using CatCommander.Resources;

namespace CatCommander.FileSystem;

/// <summary>
/// Optional destination-side contract used for cross-provider transfers. Read-only archive
/// providers intentionally do not implement it.
/// </summary>
public interface IWritableResourceProvider
{
    Task<ResourceRef> CreateDirectoryResourceAsync(
        ResourceRef parent,
        string name,
        CancellationToken ct = default);

    Task<Stream> OpenWriteAsync(
        ResourceRef parent,
        string name,
        CancellationToken ct = default);
}
