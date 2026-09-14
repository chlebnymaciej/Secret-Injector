namespace VaultInjector.Core.Models;

/// <summary>Root, non-secret application configuration persisted as JSON. The Vault token is never stored here.</summary>
public sealed class AppConfig
{
    public VaultConnectionSettings Vault { get; set; } = new();

    public HotkeyDefinition MenuHotkey { get; set; } = new();

    public List<SecretEntry> Secrets { get; set; } = new();

    public bool StartWithWindows { get; set; }

    /// <summary>Restore whatever was previously on the clipboard after the paste completes.</summary>
    public bool RestoreClipboardAfterPaste { get; set; } = true;

    public int ClipboardRestoreDelayMs { get; set; } = 1500;

    /// <summary>Serilog level name: Verbose, Debug, Information, Warning, Error, Fatal.</summary>
    public string LogLevel { get; set; } = "Information";

    public int LogRetainedDays { get; set; } = 14;
}
