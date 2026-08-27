using CatCommander.Browsing;
using CatCommander.Models;
using CatCommander.Resources;

namespace CatCommander.FileSystem;

/// <summary>Copies between a readable source provider and an independently writable destination.</summary>
public sealed class ResourceTransferService
{
    public async Task CopyAsync(
        BrowserItem source,
        ContainerRef destination,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        EnsureCopyAllowed(source, destination);

        if (ReferenceEquals(source.Resource.Provider, destination.Resource.Provider))
        {
            await source.Resource.Provider.CopyAsync(
                source.Resource.Path,
                destination.Resource.Path,
                progress,
                ct);
            return;
        }

        if (destination.Resource.Provider is not IWritableResourceProvider writer)
            throw new NotSupportedException($"Provider '{destination.Resource.ProviderId}' is not writable.");

        await CopyAcrossProvidersAsync(source, destination.Resource, writer, progress, ct);
    }

    public async Task MoveAsync(
        BrowserItem source,
        ContainerRef destination,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        EnsureCopyAllowed(source, destination);
        if (!source.Capabilities.HasFlag(ResourceCapabilities.Delete))
            throw new NotSupportedException($"'{source.Item.Name}' cannot be removed from its source provider.");

        if (ReferenceEquals(source.Resource.Provider, destination.Resource.Provider))
        {
            await source.Resource.Provider.MoveAsync(
                source.Resource.Path,
                destination.Resource.Path,
                progress,
                ct);
            return;
        }

        await CopyAsync(source, destination, progress, ct);
        await source.Resource.Provider.DeleteAsync(source.Resource.Path, ct);
    }

    public Task DeleteAsync(BrowserItem item, CancellationToken ct = default)
    {
        if (!item.Capabilities.HasFlag(ResourceCapabilities.Delete))
            throw new NotSupportedException($"'{item.Item.Name}' cannot be deleted from its provider.");
        return item.Resource.Provider.DeleteAsync(item.Resource.Path, ct);
    }

    private static async Task CopyAcrossProvidersAsync(
        BrowserItem source,
        ResourceRef destination,
        IWritableResourceProvider writer,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (source.Item.ItemType == FileSystemItemType.Directory)
        {
            var created = await writer.CreateDirectoryResourceAsync(destination, source.Item.Name, ct);
            var children = await source.Resource.Provider.ListChildrenAsync(source.Resource.Path, ct);
            foreach (var child in children)
            {
                var browserItem = BrowserItemFactory.Create(
                    source.Resource.Provider,
                    child,
                    source.Resource);
                await CopyAcrossProvidersAsync(browserItem, created, writer, progress, ct);
            }
        }
        else
        {
            await using var input = await source.Resource.Provider.OpenReadAsync(source.Resource.Path, ct);
            await using var output = await writer.OpenWriteAsync(destination, source.Item.Name, ct);
            await input.CopyToAsync(output, ct);
            progress?.Report(source.Item.Name);
        }
    }

    private static void EnsureCopyAllowed(BrowserItem source, ContainerRef destination)
    {
        if (!source.Capabilities.HasFlag(ResourceCapabilities.Read) &&
            source.Item.ItemType != FileSystemItemType.Directory)
        {
            throw new NotSupportedException($"'{source.Item.Name}' is not readable.");
        }

        var required = source.Item.ItemType == FileSystemItemType.Directory
            ? ContainerCapabilities.AcceptDirectories
            : ContainerCapabilities.AcceptFiles;
        if (!destination.Capabilities.HasFlag(required))
            throw new NotSupportedException("The destination does not accept this resource type.");
    }
}
