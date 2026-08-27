using CatCommander.Models;
using CatCommander.Resources;

namespace CatCommander.Browsing;

/// <summary>
/// One displayed result with stable provenance. Container is deliberately explicit because a
/// projected listing (search/expanded results) cannot derive it from one shared CurrentPath.
/// </summary>
public sealed record BrowserItem(
    IFileSystemItem Item,
    ResourceRef Resource,
    ResourceRef? Container,
    ResourceCapabilities Capabilities,
    int Depth = 0);
