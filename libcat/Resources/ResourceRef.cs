using CatCommander.FileSystem;

namespace CatCommander.Resources;

/// <summary>
/// An address together with the provider instance that owns its namespace. A path by itself is not
/// a cross-provider identity: search and expanded listings may contain local, SFTP, and archive
/// entries side by side.
/// </summary>
public readonly record struct ResourceRef(IFileSystemProvider Provider, string Path)
{
    public string ProviderId => Provider.Id;
}

/// <summary>A resource known to be usable as a transfer destination container.</summary>
public readonly record struct ContainerRef(ResourceRef Resource, ContainerCapabilities Capabilities);
