namespace VaultInjector.Core.Models;

public sealed class VaultConnectionSettings
{
    /// <summary>Base URL of the Vault server, e.g. https://vault.example.com:8200</summary>
    public string Address { get; set; } = "https://127.0.0.1:8200";

    /// <summary>Optional Vault Enterprise namespace. Leave null/empty for OSS or the root namespace.</summary>
    public string? Namespace { get; set; }

    /// <summary>Mount point of the KV secrets engine, e.g. "secret".</summary>
    public string MountPath { get; set; } = "secret";

    public KvVersion KvVersion { get; set; } = KvVersion.V2;

    /// <summary>Path prefix under the mount that every configured secret entry is relative to, e.g. "myapp/prod".</summary>
    public string BasePath { get; set; } = string.Empty;

    /// <summary>
    /// Only disable TLS verification for trusted internal test environments. Never disable it against
    /// a production Vault reachable over an untrusted network.
    /// </summary>
    public bool SkipTlsVerify { get; set; }
}
