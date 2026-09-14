using System.IO;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace VaultInjector.App.Logging;

/// <summary>
/// Wires up Serilog file logging. The minimum level is behind a <see cref="LoggingLevelSwitch"/> so the
/// Settings window can change verbosity immediately without tearing down the logging pipeline.
/// </summary>
public static class LoggingBootstrapper
{
    private static readonly LoggingLevelSwitch LevelSwitch = new(LogEventLevel.Information);

    public static ILoggerFactory Initialize(string logsDirectory, string levelName, int retainedDays)
    {
        Directory.CreateDirectory(logsDirectory);
        LevelSwitch.MinimumLevel = ParseLevel(levelName);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(LevelSwitch)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logsDirectory, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: Math.Max(retainedDays, 1),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return new SerilogLoggerFactory(Log.Logger, dispose: true);
    }

    /// <summary>Changing the level from Settings applies immediately; changing retained-days needs a restart.</summary>
    public static void SetLevel(string levelName) => LevelSwitch.MinimumLevel = ParseLevel(levelName);

    private static LogEventLevel ParseLevel(string levelName) =>
        Enum.TryParse<LogEventLevel>(levelName, ignoreCase: true, out var level) ? level : LogEventLevel.Information;
}
