using CatCommander.Browsing;
using CatCommander.Models;
using CatCommander.Resources;
using CatCommander.Platform;

namespace CatCommander.FileSystem;

/// <summary>Copies between a readable source provider and an independently writable destination.</summary>
public sealed class ResourceTransferService
{
    private readonly bool _windowsCopyNames;

    public ResourceTransferService(bool? windowsCopyNames = null, PlatformInfo? platform = null)
    {
        _windowsCopyNames = windowsCopyNames ?? (platform ?? PlatformInfo.Current).IsWindows;
    }

    public async Task CopyAsync(
        BrowserItem source,
        ContainerRef destination,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        EnsureCopyAllowed(source, destination);

        var targetName = await GetAvailableCopyNameAsync(source, destination, ct);
        if (targetName == source.Item.Name &&
            source.Resource.Provider.IsSameFileSystem(destination.Resource.Provider) &&
            source.Resource.Provider is INativeResourceTransferProvider nativeTransfer)
        {
            await nativeTransfer.CopyAsync(
                source.Resource.Path,
                destination.Resource.Path,
                progress,
                ct);
            return;
        }

        if (destination.Resource.Provider is not IWritableResourceProvider writer)
            throw new NotSupportedException($"Provider '{destination.Resource.ProviderId}' is not writable.");

        await CopyAcrossProvidersAsync(source, destination.Resource, writer, targetName, progress, ct);
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

        if (source.Resource.Provider.IsSameFileSystem(destination.Resource.Provider) &&
            source.Resource.Provider is INativeResourceTransferProvider nativeTransfer)
        {
            await nativeTransfer.MoveAsync(
                source.Resource.Path,
                destination.Resource.Path,
                progress,
                ct);
            return;
        }

        await CopyAsync(source, destination, progress, ct);
        if (source.Resource.Provider is not IResourceMutationProvider mutation)
            throw new NotSupportedException($"Provider '{source.Resource.ProviderId}' cannot remove the move source.");
        await mutation.DeleteAsync(source.Resource.Path, ct);
    }

    public Task DeleteAsync(BrowserItem item, CancellationToken ct = default)
    {
        if (!item.Capabilities.HasFlag(ResourceCapabilities.Delete))
            throw new NotSupportedException($"'{item.Item.Name}' cannot be deleted from its provider.");
        return item.Resource.Provider is IResourceMutationProvider mutation
            ? mutation.DeleteAsync(item.Resource.Path, ct)
            : Task.FromException(new NotSupportedException($"Provider '{item.Resource.ProviderId}' cannot delete resources."));
    }

    private static async Task CopyAcrossProvidersAsync(
        BrowserItem source,
        ResourceRef destination,
        IWritableResourceProvider writer,
        string targetName,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (source.Item.ItemType == FileSystemItemType.Directory)
        {
            var created = await writer.CreateDirectoryResourceAsync(destination, targetName, ct);
            var children = await source.Resource.Provider.ListChildrenAsync(source.Resource.Path, ct);
            foreach (var child in children)
            {
                var browserItem = BrowserItemFactory.Create(
                    source.Resource.Provider,
                    child,
                    source.Resource);
                await CopyAcrossProvidersAsync(browserItem, created, writer, child.Name, progress, ct);
            }
        }
        else
        {
            await using var input = await source.Resource.Provider.OpenReadAsync(source.Resource.Path, ct);
            await using var output = await writer.OpenWriteAsync(destination, targetName, ct);
            await input.CopyToAsync(output, ct);
            progress?.Report(source.Item.Name);
        }
    }

    private async Task<string> GetAvailableCopyNameAsync(
        BrowserItem source,
        ContainerRef destination,
        CancellationToken ct)
    {
        var children = await destination.Resource.Provider.ListChildrenAsync(destination.Resource.Path, ct);
        var names = children.Select(item => item.Name).ToHashSet(destination.Resource.Provider.NameComparer);
        if (!names.Contains(source.Item.Name))
            return source.Item.Name;

        var extension = source.Item.ItemType == FileSystemItemType.File
            ? FileNameUtility.GetExtension(source.Item.Name)
            : string.Empty;
        var stem = extension.Length > 0 ? source.Item.Name[..^extension.Length] : source.Item.Name;
        for (var copyNumber = 1; ; copyNumber++)
        {
            var suffix = _windowsCopyNames
                ? $" ({copyNumber})"
                : copyNumber == 1 ? "_副本" : $"_副本{copyNumber}";
            var candidate = stem + suffix + extension;
            if (!names.Contains(candidate))
                return candidate;
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
