using VaultInjector.Core.Models;

namespace VaultInjector.Core.Services;

public static class VaultPathBuilder
{
    /// <summary>Joins the configured base path with a secret entry's relative path, tolerating leading/trailing slashes.</summary>
    public static string BuildLogicalPath(VaultConnectionSettings settings, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("Relative path must not be empty.", nameof(relativePath));
        }

        var basePath = settings.BasePath?.Trim('/') ?? string.Empty;
        var relative = relativePath.Trim('/');

        return string.IsNullOrEmpty(basePath) ? relative : $"{basePath}/{relative}";
    }
}
