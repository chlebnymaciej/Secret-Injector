using Microsoft.Extensions.Logging;
using VaultInjector.Core.Models;
using VaultInjector.Core.Services;

namespace VaultInjector.App.Services;

/// <summary>
/// Holds the app's live, mutable configuration plus the shared services, and notifies interested
/// parties (hotkey registration, tray menu) whenever settings are saved.
/// </summary>
public sealed class AppRuntime
{
    private readonly ILogger<AppRuntime> _logger;

    public IConfigStore ConfigStore { get; }
    public ITokenStore TokenStore { get; }
    public IVaultService VaultService { get; }
    public AutoStartService AutoStartService { get; }
    public string LogsDirectory { get; }

    public AppConfig Config { get; private set; }

    public event EventHandler? ConfigChanged;

    public AppRuntime(
        IConfigStore configStore,
        ITokenStore tokenStore,
        IVaultService vaultService,
        AutoStartService autoStartService,
        string logsDirectory,
        ILogger<AppRuntime> logger)
    {
        ConfigStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        TokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        VaultService = vaultService ?? throw new ArgumentNullException(nameof(vaultService));
        AutoStartService = autoStartService ?? throw new ArgumentNullException(nameof(autoStartService));
        LogsDirectory = logsDirectory ?? throw new ArgumentNullException(nameof(logsDirectory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        Config = ConfigStore.Load();
    }

    public void SaveConfig(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        ConfigStore.Save(config);
        Config = config;
        _logger.LogInformation("Configuration saved ({SecretCount} secret entries)", config.Secrets.Count);
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadConfig()
    {
        Config = ConfigStore.Load();
        _logger.LogInformation("Configuration reloaded from disk");
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
