using CatCommander.Browsing;
using CatCommander.Models;
using CatCommander.Resources;
using CatCommander.ViewModels;

namespace CatCommander.Services;

/// <summary>One capability policy shared by shortcut, menu, and file-operation entry points.</summary>
public sealed class BrowserCommandPolicy
{
    public bool CanCopy(IReadOnlyList<BrowserItem> sources, ContainerRef? destination) =>
        destination is { } target && sources.Count > 0 && sources.All(source =>
            SourceCanBeCopied(source) && DestinationAccepts(source, target));

    public bool CanMove(IReadOnlyList<BrowserItem> sources, ContainerRef? destination) =>
        CanCopy(sources, destination) &&
        sources.All(source => source.Capabilities.HasFlag(ResourceCapabilities.Delete));

    public bool CanDelete(IReadOnlyList<BrowserItem> sources) =>
        sources.Count > 0 && sources.All(source =>
            source.Capabilities.HasFlag(ResourceCapabilities.Delete));

    public bool CanCreateDirectory(BrowserContext? context) =>
        context?.WritableDestination is { } destination &&
        destination.Capabilities.HasFlag(ContainerCapabilities.CreateDirectory);

    public bool CanExpand(IEnumerable<BrowserItem> sources) =>
        sources.Any(source => source.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren));

    public bool CanRunFileOperation(
        FileOperationKind kind,
        IReadOnlyList<BrowserItem> sources,
        ContainerRef? destination) => kind switch
    {
        FileOperationKind.Copy => CanCopy(sources, destination),
        FileOperationKind.Move => CanMove(sources, destination),
        FileOperationKind.Delete => CanDelete(sources),
        _ => false,
    };

    private static bool SourceCanBeCopied(BrowserItem source) =>
        source.Item.ItemType == FileSystemItemType.Directory
            ? source.Capabilities.HasFlag(ResourceCapabilities.EnumerateChildren)
            : source.Capabilities.HasFlag(ResourceCapabilities.Read);

    private static bool DestinationAccepts(BrowserItem source, ContainerRef destination)
    {
        var required = source.Item.ItemType == FileSystemItemType.Directory
            ? ContainerCapabilities.AcceptDirectories
            : ContainerCapabilities.AcceptFiles;
        return destination.Capabilities.HasFlag(required);
    }
}
