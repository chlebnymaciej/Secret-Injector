using System.Windows.Forms;
using Icon = System.Drawing.Icon;

namespace VaultInjector.App.Services;

/// <summary>Owns the tray icon and its right-click menu. The app has no taskbar window - this is the UI.</summary>
public sealed class TrayIconManager : IDisposable
{
    private const int IconPixelSize = 32;

    private readonly NotifyIcon _notifyIcon;
    private Icon _currentIcon;

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

        _currentIcon = TrayIconRenderer.RenderKeyIcon(IconPixelSize, isLoggedIn: null);
        _notifyIcon = new NotifyIcon
        {
            Icon = _currentIcon,
            Text = "Vault Injector",
            Visible = true,
            ContextMenuStrip = menu
        };

        _notifyIcon.DoubleClick += (_, _) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Redraws the tray icon with a green/red status dot reflecting whether Vault is currently logged in.</summary>
    public void SetLoginStatus(bool isLoggedIn)
    {
        var newIcon = TrayIconRenderer.RenderKeyIcon(IconPixelSize, isLoggedIn);
        var previousIcon = _currentIcon;

        _notifyIcon.Icon = newIcon;
        _notifyIcon.Text = isLoggedIn ? "Vault Injector - Logged in" : "Vault Injector - Not logged in";
        _currentIcon = newIcon;

        previousIcon.Dispose();
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
        _currentIcon.Dispose();
    }
}
