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
        return new BrowserItem(
            entry,
            new ResourceRef(provider, entry.FullPath),
            container,
            provider.GetResourceCapabilities(entry, container),
            depth);
    }
}
