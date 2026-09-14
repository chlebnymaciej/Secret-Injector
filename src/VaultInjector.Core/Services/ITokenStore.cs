namespace VaultInjector.Core.Services;

/// <summary>
/// Persists the Vault token on disk, encrypted at rest. The Windows implementation lives in the App
/// project (DPAPI, user-scoped) since Core stays platform-agnostic and unit-testable.
/// </summary>
public interface ITokenStore
{
    bool HasToken { get; }
    string? LoadToken();
    void SaveToken(string token);
    void ClearToken();
}
