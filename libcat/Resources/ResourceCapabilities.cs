namespace CatCommander.Resources;

[Flags]
public enum ResourceCapabilities
{
    None = 0,
    Read = 1 << 0,
    EnumerateChildren = 1 << 1,
    Rename = 1 << 2,
    Delete = 1 << 3,
    OpenExternally = 1 << 4,
}

[Flags]
public enum ContainerCapabilities
{
    None = 0,
    AcceptFiles = 1 << 0,
    AcceptDirectories = 1 << 1,
    CreateDirectory = 1 << 2,
}
