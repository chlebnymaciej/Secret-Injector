using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Serilog;
using VaultInjector.App.Logging;
using VaultInjector.App.Services;
using VaultInjector.App.Views;
using VaultInjector.Core.Services;
using Application = System.Windows.Application;

namespace VaultInjector.App;

public partial class App : Application
{
    private ILoggerFactory _loggerFactory = null!;
    private AppRuntime _runtime = null!;
    private TrayIconManager _trayIconManager = null!;
    private GlobalHotkeyService _hotkeyService = null!;
    private SecretMenuController _menuController = null!;
    private SettingsWindow? _settingsWindow;

    private static readonly string AppDataDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VaultInjector");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Needed for the WinForms tray menu (NotifyIcon/ContextMenuStrip) to render with modern visual styles.
        System.Windows.Forms.Application.EnableVisualStyles();

        var configFilePath = Path.Combine(AppDataDirectory, "config.json");
        var tokenFilePath = Path.Combine(AppDataDirectory, "token.dat");
        var logsDirectory = Path.Combine(AppDataDirectory, "logs");

        var configStore = new JsonConfigStore(configFilePath);
        var bootstrapConfig = configStore.Load();

        _loggerFactory = LoggingBootstrapper.Initialize(logsDirectory, bootstrapConfig.LogLevel, bootstrapConfig.LogRetainedDays);
        var startupLogger = _loggerFactory.CreateLogger<App>();
        startupLogger.LogInformation("Vault Injector starting up");

        var tokenStore = new DpapiTokenStore(tokenFilePath, _loggerFactory.CreateLogger<DpapiTokenStore>());
        var vaultService = new VaultService(_loggerFactory.CreateLogger<VaultService>());
        var autoStartService = new AutoStartService(_loggerFactory.CreateLogger<AutoStartService>());

        _runtime = new AppRuntime(configStore, tokenStore, vaultService, autoStartService, logsDirectory, _loggerFactory.CreateLogger<AppRuntime>());
        _runtime.ConfigChanged += (_, _) =>
        {
            LoggingBootstrapper.SetLevel(_runtime.Config.LogLevel);
            _hotkeyService.Register(_runtime.Config.MenuHotkey);
        };

        _trayIconManager = new TrayIconManager();
        _trayIconManager.OpenSettingsRequested += (_, _) => ShowSettingsWindow();
        _trayIconManager.ReloadConfigRequested += (_, _) => _runtime.ReloadConfig();
        _trayIconManager.OpenLogsFolderRequested += (_, _) => OpenLogsFolder();
        _trayIconManager.ExitRequested += (_, _) => Shutdown();

        var pasteService = new ClipboardPasteService(_loggerFactory.CreateLogger<ClipboardPasteService>());
        _menuController = new SecretMenuController(
            _runtime,
            pasteService,
            (text, icon) => _trayIconManager.ShowBalloon(text, icon),
            _loggerFactory.CreateLogger<SecretMenuController>());

        _hotkeyService = new GlobalHotkeyService(_loggerFactory.CreateLogger<GlobalHotkeyService>());
        _hotkeyService.HotkeyPressed += (_, _) => _menuController.ShowMenu();

        if (!_hotkeyService.Register(_runtime.Config.MenuHotkey))
        {
            _trayIconManager.ShowBalloon(
                "Could not register the paste-menu hotkey - it may be in use by another application. Change it in Settings.",
                ToolTipIcon.Warning);
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (!tokenStore.HasToken)
        {
            _trayIconManager.ShowBalloon("Vault Injector is running. Open Settings to log in to Vault.", ToolTipIcon.Info);
        }
    }

    private void ShowSettingsWindow()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_runtime, _loggerFactory);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    private void OpenLogsFolder()
    {
        try
        {
            Directory.CreateDirectory(_runtime.LogsDirectory);
            Process.Start(new ProcessStartInfo { FileName = _runtime.LogsDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _loggerFactory.CreateLogger<App>().LogWarning(ex, "Failed to open logs folder");
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _loggerFactory.CreateLogger<App>().LogError(e.Exception, "Unhandled UI exception");
        _trayIconManager.ShowBalloon($"Unexpected error: {e.Exception.Message}", ToolTipIcon.Error);
        e.Handled = true;
    }

    /// <summary>
    /// Last-resort net for exceptions raised off the WPF dispatcher thread (background/thread-pool work).
    /// The CLR terminates the process right after this fires when <see cref="UnhandledExceptionEventArgs.IsTerminating"/>
    /// is true - there's no way to prevent that, so this only logs it before the process goes down.
    /// </summary>
    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var logger = _loggerFactory.CreateLogger<App>();
        if (e.ExceptionObject is Exception ex)
        {
            logger.LogCritical(ex, "Unhandled non-UI exception (IsTerminating={IsTerminating})", e.IsTerminating);
        }
        else
        {
            logger.LogCritical("Unhandled non-UI exception of unknown type: {ExceptionObject} (IsTerminating={IsTerminating})", e.ExceptionObject, e.IsTerminating);
        }

        Log.CloseAndFlush();
    }

    /// <summary>Catches exceptions from fire-and-forget Tasks whose faults nobody awaited/observed.</summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _loggerFactory.CreateLogger<App>().LogError(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayIconManager?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
