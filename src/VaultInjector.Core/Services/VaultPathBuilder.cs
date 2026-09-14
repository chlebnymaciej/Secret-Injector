using VaultInjector.Core.Models;

namespace VaultInjector.Core.Services;

public static class VaultPathBuilder
{
    /// <summary>Joins the configured base path with a secret entry's relative path, tolerating leading/trailing slashes.</summary>
    /// <param name="allowEmptyRelativePath">
    /// When true, an empty <paramref name="relativePath"/> resolves to the base path itself instead of throwing -
    /// used when listing from the root of the base path.
    /// </param>
    public static string BuildLogicalPath(VaultConnectionSettings settings, string relativePath, bool allowEmptyRelativePath = false)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var basePath = settings.BasePath?.Trim('/') ?? string.Empty;
        var relative = relativePath?.Trim().Trim('/') ?? string.Empty;

        if (relative.Length == 0)
        {
            if (!allowEmptyRelativePath)
            {
                throw new ArgumentException("Relative path must not be empty.", nameof(relativePath));
            }

            return basePath;
        }

        return string.IsNullOrEmpty(basePath) ? relative : $"{basePath}/{relative}";
    }
}
