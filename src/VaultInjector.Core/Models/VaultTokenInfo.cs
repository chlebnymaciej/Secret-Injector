namespace VaultInjector.Core.Models;

/// <summary>Result of validating a token against Vault (auth/token/lookup-self).</summary>
public sealed class VaultTokenInfo
{
    public bool IsValid { get; init; }
    public string? DisplayName { get; init; }
    public IReadOnlyList<string> Policies { get; init; } = Array.Empty<string>();
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? Error { get; init; }

    public static VaultTokenInfo Invalid(string error) => new() { IsValid = false, Error = error };
}
