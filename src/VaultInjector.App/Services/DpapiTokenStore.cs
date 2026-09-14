using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using VaultInjector.Core.Services;

namespace VaultInjector.App.Services;

/// <summary>
/// Persists the Vault token encrypted with Windows DPAPI, scoped to the current Windows user account.
/// Only that user, on this machine, can decrypt it - not other users of the machine, and not the file
/// if copied elsewhere.
/// </summary>
public sealed class DpapiTokenStore : ITokenStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VaultInjector.TokenStore.v1");

    private readonly string _tokenFilePath;
    private readonly ILogger<DpapiTokenStore> _logger;

    public DpapiTokenStore(string tokenFilePath, ILogger<DpapiTokenStore> logger)
    {
        _tokenFilePath = tokenFilePath ?? throw new ArgumentNullException(nameof(tokenFilePath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool HasToken => File.Exists(_tokenFilePath);

    public string? LoadToken()
    {
        if (!File.Exists(_tokenFilePath))
        {
            return null;
        }

        try
        {
            var encrypted = File.ReadAllBytes(_tokenFilePath);
            var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt stored Vault token; treating as logged out");
            return null;
        }
    }

    public void SaveToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var directory = Path.GetDirectoryName(_tokenFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var plain = Encoding.UTF8.GetBytes(token);
        var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_tokenFilePath, encrypted);
        _logger.LogInformation("Vault token stored (encrypted, current user only)");
    }

    public void ClearToken()
    {
        if (File.Exists(_tokenFilePath))
        {
            File.Delete(_tokenFilePath);
            _logger.LogInformation("Vault token cleared");
        }
    }
}
