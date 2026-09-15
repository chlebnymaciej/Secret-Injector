using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VaultInjector.Core.Models;

namespace VaultInjector.App.Services;

/// <summary>
/// Periodically re-validates the stored Vault token against auth/token/lookup-self, drives the tray
/// icon's logged-in/out indicator, and warns the user once as the token approaches expiry (and again if
/// it actually expires or otherwise stops being valid) instead of letting pastes silently start failing.
/// </summary>
public sealed class TokenStatusMonitor : IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ExpiryWarningWindow = TimeSpan.FromMinutes(15);

    private readonly AppRuntime _runtime;
    private readonly Action<string, ToolTipIcon> _notify;
    private readonly ILogger<TokenStatusMonitor> _logger;
    private readonly DispatcherTimer _timer;

    private int? _lastCheckedTokenHash;
    private bool _expiryWarningShown;
    private bool _invalidNoticeShown;

    /// <summary>Raised whenever a check completes with a definitive logged-in/out result.</summary>
    public event EventHandler<bool>? LoginStatusChanged;

    public TokenStatusMonitor(AppRuntime runtime, Action<string, ToolTipIcon> notify, ILogger<TokenStatusMonitor> logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _timer = new DispatcherTimer { Interval = CheckInterval };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start()
    {
        _timer.Start();
        _ = CheckAsync();
    }

    public async Task CheckAsync()
    {
        var token = _runtime.TokenStore.LoadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _lastCheckedTokenHash = null;
            ResetWarnings();
            LoginStatusChanged?.Invoke(this, false);
            return;
        }

        // A different token than we last checked (fresh login) - any warnings shown so far no longer apply.
        var tokenHash = token.GetHashCode();
        if (tokenHash != _lastCheckedTokenHash)
        {
            _lastCheckedTokenHash = tokenHash;
            ResetWarnings();
        }

        VaultTokenInfo info;
        try
        {
            info = await _runtime.VaultService.ValidateTokenAsync(_runtime.Config.Vault, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vault token status check failed");
            return;
        }

        if (!info.IsValid)
        {
            LoginStatusChanged?.Invoke(this, false);
            if (!_invalidNoticeShown)
            {
                _invalidNoticeShown = true;
                _logger.LogWarning("Vault token is no longer valid: {Error}", info.Error);
                _notify($"Vault token is no longer valid: {info.Error}", ToolTipIcon.Error);
            }

            return;
        }

        LoginStatusChanged?.Invoke(this, true);
        _invalidNoticeShown = false;

        if (info.ExpiresAt is not { } expiresAt)
        {
            return;
        }

        var remaining = expiresAt - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            if (!_invalidNoticeShown)
            {
                _invalidNoticeShown = true;
                _logger.LogWarning("Vault token has expired (expired at {ExpiresAt})", expiresAt);
                _notify("Vault token has expired. Log in again from Settings.", ToolTipIcon.Error);
            }
        }
        else if (remaining <= ExpiryWarningWindow && !_expiryWarningShown)
        {
            _expiryWarningShown = true;
            _logger.LogInformation("Vault token expires soon, at {ExpiresAt}", expiresAt);
            _notify($"Vault token expires soon, at {expiresAt.ToLocalTime():t}. Log in again from Settings before then.", ToolTipIcon.Warning);
        }
    }

    private void ResetWarnings()
    {
        _expiryWarningShown = false;
        _invalidNoticeShown = false;
    }

    public void Dispose() => _timer.Stop();
}
