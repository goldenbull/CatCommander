namespace CatCommander.FileSystem;

/// <summary>Maps a provider-local location to a registry-resolvable/displayable application path.</summary>
public interface IExternalPathProvider
{
    string GetExternalPath(string providerPath);
}
