using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VaultSharp;
using VaultSharp.Core;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultInjector.Core.Models;

namespace VaultInjector.Core.Services;

/// <summary>
/// Thin wrapper around VaultSharp that logs every call (path, HTTP outcome, elapsed time) without ever
/// logging secret values or the token itself.
/// </summary>
public sealed class VaultService : IVaultService
{
    private readonly ILogger<VaultService> _logger;

    public VaultService(ILogger<VaultService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VaultTokenInfo> ValidateTokenAsync(VaultConnectionSettings settings, string token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(token))
        {
            return VaultTokenInfo.Invalid("No token provided.");
        }

        const string operation = "auth/token/lookup-self";
        var client = CreateClient(settings, token);
        var sw = Stopwatch.StartNew();

        try
        {
            var result = await client.V1.Auth.Token.LookupSelfAsync().ConfigureAwait(false);
            sw.Stop();
            LogCall(operation, "GET", 200, sw.ElapsedMilliseconds);

            DateTimeOffset? expiresAt = null;
            if (!string.IsNullOrEmpty(result.Data.ExpireTime) && DateTimeOffset.TryParse(result.Data.ExpireTime, out var parsed))
            {
                expiresAt = parsed;
            }

            return new VaultTokenInfo
            {
                IsValid = true,
                DisplayName = result.Data.DisplayName,
                Policies = result.Data.Policies ?? new List<string>(),
                ExpiresAt = expiresAt
            };
        }
        catch (VaultApiException vex)
        {
            sw.Stop();
            LogCall(operation, "GET", vex.StatusCode, sw.ElapsedMilliseconds, vex);
            return VaultTokenInfo.Invalid(DescribeVaultApiException(vex));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            LogCall(operation, "GET", null, sw.ElapsedMilliseconds, ex);
            return VaultTokenInfo.Invalid(ex.Message);
        }
    }

    public async Task<IReadOnlyDictionary<string, object?>> ReadSecretDataAsync(VaultConnectionSettings settings, string token, string relativePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new VaultServiceException("No Vault token available. Log in first.");
        }

        var logicalPath = VaultPathBuilder.BuildLogicalPath(settings, relativePath);
        var operation = $"secrets/{settings.MountPath}/{logicalPath} (KV v{(int)settings.KvVersion})";
        var client = CreateClient(settings, token);
        var sw = Stopwatch.StartNew();

        try
        {
            IDictionary<string, object> data;
            if (settings.KvVersion == KvVersion.V2)
            {
                var secret = await client.V1.Secrets.KeyValue.V2
                    .ReadSecretAsync(path: logicalPath, mountPoint: settings.MountPath)
                    .ConfigureAwait(false);
                data = secret.Data.Data;
            }
            else
            {
                var secret = await client.V1.Secrets.KeyValue.V1
                    .ReadSecretAsync(path: logicalPath, mountPoint: settings.MountPath)
                    .ConfigureAwait(false);
                data = secret.Data;
            }

            sw.Stop();
            LogCall(operation, "GET", 200, sw.ElapsedMilliseconds, fieldCount: data.Count);

            return data.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);
        }
        catch (VaultApiException vex)
        {
            sw.Stop();
            LogCall(operation, "GET", vex.StatusCode, sw.ElapsedMilliseconds, vex);
            throw new VaultServiceException(DescribeVaultApiException(vex), vex.StatusCode, vex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not VaultServiceException)
        {
            sw.Stop();
            LogCall(operation, "GET", null, sw.ElapsedMilliseconds, ex);
            throw new VaultServiceException($"Failed to read secret '{relativePath}': {ex.Message}", null, ex);
        }
    }

    public async Task<string> ResolveSecretValueAsync(VaultConnectionSettings settings, string token, SecretEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var data = await ReadSecretDataAsync(settings, token, entry.RelativePath, cancellationToken).ConfigureAwait(false);

        if (entry.Mode == SecretFetchMode.WholeSecretAsJson)
        {
            return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        }

        if (string.IsNullOrWhiteSpace(entry.FieldName))
        {
            throw new VaultServiceException($"Secret entry '{entry.Alias}' is set to paste a single field but no field name is configured.");
        }

        if (!data.TryGetValue(entry.FieldName, out var value) || value is null)
        {
            throw new VaultServiceException($"Field '{entry.FieldName}' was not found in secret '{entry.RelativePath}'.");
        }

        return StringifyFieldValue(value);
    }

    private static string StringifyFieldValue(object value)
    {
        if (value is string str)
        {
            return str;
        }

        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => element.GetRawText()
            };
        }

        return value.ToString() ?? string.Empty;
    }

    private static IVaultClient CreateClient(VaultConnectionSettings settings, string token)
    {
        IAuthMethodInfo authMethod = new TokenAuthMethodInfo(token);
        var clientSettings = new VaultClientSettings(settings.Address, authMethod)
        {
            Namespace = string.IsNullOrWhiteSpace(settings.Namespace) ? null : settings.Namespace
        };

        if (settings.SkipTlsVerify)
        {
            clientSettings.PostProcessHttpClientHandlerAction = handler =>
            {
                if (handler is HttpClientHandler httpClientHandler)
                {
                    httpClientHandler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                }
            };
        }

        return new VaultClient(clientSettings);
    }

    private static string DescribeVaultApiException(VaultApiException vex)
    {
        var errors = vex.ApiErrors?.ToArray() ?? Array.Empty<string>();
        var detail = errors.Length > 0 ? string.Join("; ", errors) : vex.Message;
        return $"Vault returned HTTP {vex.StatusCode}: {detail}";
    }

    private void LogCall(string operation, string httpMethod, int? statusCode, long elapsedMs, Exception? exception = null, int? fieldCount = null)
    {
        if (exception is null)
        {
            _logger.LogInformation(
                "Vault call {HttpMethod} {Operation} completed in {ElapsedMs}ms with status {StatusCode} fields={FieldCount}",
                httpMethod, operation, elapsedMs, statusCode, fieldCount);
        }
        else
        {
            _logger.LogWarning(
                exception,
                "Vault call {HttpMethod} {Operation} failed after {ElapsedMs}ms with status {StatusCode}",
                httpMethod, operation, elapsedMs, statusCode);
        }
    }
}
