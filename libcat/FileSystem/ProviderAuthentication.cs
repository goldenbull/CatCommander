namespace CatCommander.FileSystem;

public enum ProviderCredentialKind
{
    Password,
    PrivateKeyPassphrase,
}

/// <summary>A non-secret key suitable for looking up one credential in a process or OS vault.</summary>
public readonly record struct ProviderCredentialKey(
    string ProviderId,
    string Account,
    ProviderCredentialKind Kind);

public sealed record ProviderAuthenticationChallenge(
    ProviderCredentialKey CredentialKey,
    string Title,
    string Prompt);

/// <summary>
/// Provider-neutral request for authentication. Navigation handles this once regardless of
/// whether the challenger is an encrypted archive, SFTP, or a future remote provider.
/// </summary>
public sealed class ProviderAuthenticationRequiredException : Exception
{
    public ProviderAuthenticationChallenge Challenge { get; }

    public ProviderAuthenticationRequiredException(
        ProviderAuthenticationChallenge challenge,
        Exception? inner = null)
        : base(challenge.Prompt, inner) => Challenge = challenge;
}

public interface IProviderCredentialStore
{
    string? Get(ProviderCredentialKey key);
    void Set(ProviderCredentialKey key, string credential);
    void Remove(ProviderCredentialKey key);
}

/// <summary>
/// Process-only credential cache. Persistent SFTP credentials will use an OS-vault implementation
/// of the same contract; secrets are never serialized into config.toml or session.toml.
/// </summary>
public sealed class ProviderCredentialStore : IProviderCredentialStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<ProviderCredentialKey, string> _credentials = new();
    public string? Get(ProviderCredentialKey key) => _credentials.GetValueOrDefault(key);
    public void Set(ProviderCredentialKey key, string credential) => _credentials[key] = credential;
    public void Remove(ProviderCredentialKey key) => _credentials.TryRemove(key, out _);
}
