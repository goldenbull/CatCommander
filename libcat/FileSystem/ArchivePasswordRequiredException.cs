namespace CatCommander.FileSystem;

public sealed class ArchivePasswordRequiredException : Exception
{
    public string ArchivePath { get; }

    public ArchivePasswordRequiredException(string archivePath, Exception? inner = null)
        : base($"A password is required to open '{Path.GetFileName(archivePath)}'.", inner) =>
        ArchivePath = archivePath;
}

public interface IArchivePasswordStore
{
    string? Get(string archivePath);
    void Set(string archivePath, string password);
}

public sealed class ArchivePasswordStore : IArchivePasswordStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _passwords =
        new(StringComparer.OrdinalIgnoreCase);
    public string? Get(string archivePath) => _passwords.GetValueOrDefault(archivePath);
    public void Set(string archivePath, string password) => _passwords[archivePath] = password;
}
