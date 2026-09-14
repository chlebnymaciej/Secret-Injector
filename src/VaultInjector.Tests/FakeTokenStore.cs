using VaultInjector.Core.Services;

namespace VaultInjector.Tests;

/// <summary>In-memory stand-in for the DPAPI-backed token store, which only runs on Windows.</summary>
internal sealed class FakeTokenStore : ITokenStore
{
    private string? _token;

    public bool HasToken => _token is not null;

    public string? LoadToken() => _token;

    public void SaveToken(string token) => _token = token;

    public void ClearToken() => _token = null;
}
