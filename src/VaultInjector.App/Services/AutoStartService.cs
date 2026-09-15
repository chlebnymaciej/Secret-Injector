using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace VaultInjector.App.Services;

/// <summary>Toggles "start with Windows" via the current user's Run registry key (no elevation needed).</summary>
public sealed class AutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VaultInjector";

    private readonly ILogger<AutoStartService> _logger;

    public AutoStartService(ILogger<AutoStartService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is not null;
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                         ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

        if (enabled)
        {
            // Environment.ProcessPath is reliably set on Windows and is checked first; the Assembly.Location
            // fallback below only exists for the (practically unreachable) case where it isn't - silencing
            // IL3000 here since a single-file publish flags Location's use even though this path never runs.
#pragma warning disable IL3000
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
#pragma warning restore IL3000
            key.SetValue(ValueName, $"\"{exePath}\"");
            _logger.LogInformation("Enabled start-with-Windows ({ExePath})", exePath);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            _logger.LogInformation("Disabled start-with-Windows");
        }
    }
}
