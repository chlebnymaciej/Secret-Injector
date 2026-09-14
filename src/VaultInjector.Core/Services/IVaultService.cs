using VaultInjector.Core.Models;

namespace VaultInjector.Core.Services;

public interface IVaultService
{
    /// <summary>Calls auth/token/lookup-self to check the token is valid and report its policies/expiry.</summary>
    Task<VaultTokenInfo> ValidateTokenAsync(VaultConnectionSettings settings, string token, CancellationToken cancellationToken = default);

    /// <summary>Reads the raw key/value data of a secret at <paramref name="relativePath"/> (relative to <see cref="VaultConnectionSettings.BasePath"/>).</summary>
    Task<IReadOnlyDictionary<string, object?>> ReadSecretDataAsync(VaultConnectionSettings settings, string token, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>Resolves the exact string that should be pasted for a configured menu entry.</summary>
    Task<string> ResolveSecretValueAsync(VaultConnectionSettings settings, string token, SecretEntry entry, CancellationToken cancellationToken = default);
}
