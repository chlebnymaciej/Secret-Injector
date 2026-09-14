using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VaultInjector.App.Native;
using Clipboard = System.Windows.Clipboard;
using IDataObject = System.Windows.IDataObject;

namespace VaultInjector.App.Services;

/// <summary>
/// Puts a secret on the clipboard, simulates Ctrl+V into whatever window last had focus, and
/// (optionally) restores the clipboard's previous contents a short delay later.
/// </summary>
public sealed class ClipboardPasteService
{
    private readonly ILogger<ClipboardPasteService> _logger;

    public ClipboardPasteService(ILogger<ClipboardPasteService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void PasteIntoWindow(string secretValue, IntPtr targetWindow, bool restoreClipboardAfter, int restoreDelayMs)
    {
        IDataObject? previousClipboard = restoreClipboardAfter ? TryCaptureClipboard() : null;

        WithClipboardRetry(() => Clipboard.SetText(secretValue));

        ForegroundWindowHelper.ForceSetForegroundWindow(targetWindow);

        // Give the OS a moment to finish switching focus before the keystrokes land.
        System.Threading.Thread.Sleep(60);
        KeyboardSimulator.SendCtrlV();

        _logger.LogInformation("Pasted secret into foreground window (restoreClipboard={RestoreClipboard})", restoreClipboardAfter);

        if (restoreClipboardAfter)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(restoreDelayMs, 200)) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                RestoreClipboard(previousClipboard);
            };
            timer.Start();
        }
    }

    private void RestoreClipboard(IDataObject? previousClipboard)
    {
        try
        {
            if (previousClipboard is not null)
            {
                WithClipboardRetry(() => Clipboard.SetDataObject(previousClipboard, true));
            }
            else
            {
                WithClipboardRetry(Clipboard.Clear);
            }

            _logger.LogDebug("Clipboard restored to its pre-paste contents");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore clipboard after paste");
        }
    }

    private IDataObject? TryCaptureClipboard()
    {
        try
        {
            return Clipboard.GetDataObject();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not capture previous clipboard contents; nothing will be restored after paste");
            return null;
        }
    }

    /// <summary>
    /// The Win32 clipboard can be transiently locked by another process (e.g. a screenshot tool);
    /// a short retry loop is the standard workaround.
    /// </summary>
    private static void WithClipboardRetry(Action action, int attempts = 10, int delayMs = 50)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                action();
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (i < attempts - 1)
            {
                System.Threading.Thread.Sleep(delayMs);
            }
        }
    }
}
