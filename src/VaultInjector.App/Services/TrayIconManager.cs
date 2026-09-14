using System.Windows.Forms;

namespace VaultInjector.App.Services;

/// <summary>Owns the tray icon and its right-click menu. The app has no taskbar window - this is the UI.</summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ReloadConfigRequested;
    public event EventHandler? OpenLogsFolderRequested;
    public event EventHandler? ExitRequested;

    public TrayIconManager()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Settings...", null, (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Reload Configuration", null, (_, _) => ReloadConfigRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add("Open Logs Folder", null, (_, _) => OpenLogsFolderRequested?.Invoke(this, EventArgs.Empty));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        _notifyIcon = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Shield,
            Text = "Vault Injector",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ShowBalloon(string text, ToolTipIcon icon, int timeoutMs = 4000)
    {
        _notifyIcon.BalloonTipTitle = "Vault Injector";
        _notifyIcon.BalloonTipText = text;
        _notifyIcon.BalloonTipIcon = icon;
        _notifyIcon.ShowBalloonTip(timeoutMs);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
