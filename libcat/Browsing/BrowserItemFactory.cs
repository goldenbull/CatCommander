using CatCommander.FileSystem;
using CatCommander.Models;
using CatCommander.Resources;

namespace CatCommander.Browsing;

internal static class BrowserItemFactory
{
    public static BrowserItem Create(
        IFileSystemProvider provider,
        IFileSystemItem entry,
        ResourceRef? container,
        int depth = 0)
    {
        var capabilities = provider.ResourceCapabilities;
        if (!provider.CanEnter(entry))
            capabilities &= ~ResourceCapabilities.EnumerateChildren;
        if (!entry.CanRead)
            capabilities &= ~ResourceCapabilities.Read;
        if (!entry.CanWrite)
            capabilities &= ~(ResourceCapabilities.Rename | ResourceCapabilities.Delete);

        return new BrowserItem(
            entry,
            new ResourceRef(provider, entry.FullPath),
            container,
            capabilities,
            depth);
    }
}
