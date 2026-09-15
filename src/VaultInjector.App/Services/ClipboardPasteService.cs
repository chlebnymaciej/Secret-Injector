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

    /// <summary>
    /// Minimum time (ms) the secret is left on the clipboard when the automatic keystroke had to be
    /// skipped, so there's a realistic window to paste it manually before it's cleared/restored.
    /// </summary>
    private const int MinManualPasteWindowMs = 15000;

    /// <returns>
    /// True if the automatic Ctrl+V was actually sent; false if it was skipped because the target window
    /// belongs to a higher-integrity process (e.g. an elevated prompt or a "Windows Security" credential
    /// dialog) - Windows' UIPI would silently drop synthetic input there, so the secret is left on the
    /// clipboard for the user to paste manually instead of pretending it worked.
    /// </returns>
    public bool PasteIntoWindow(string secretValue, IntPtr targetWindow, bool restoreClipboardAfter, int restoreDelayMs)
    {
        IDataObject? previousClipboard = restoreClipboardAfter ? TryCaptureClipboard() : null;

        WithClipboardRetry(() => Clipboard.SetText(secretValue));

        var requiresManualPaste = ProcessIntegrity.IsHigherIntegrityThanSelf(targetWindow);
        if (requiresManualPaste)
        {
            _logger.LogInformation(
                "Target window belongs to a higher-integrity process (e.g. an elevated or credential-prompt window) - " +
                "a simulated Ctrl+V would be silently blocked by Windows, so leaving the secret on the clipboard for a manual paste instead");
        }
        else
        {
            ForegroundWindowHelper.ForceSetForegroundWindow(targetWindow);

            // Give the OS a moment to finish switching focus before the keystrokes land.
            System.Threading.Thread.Sleep(60);
            KeyboardSimulator.SendCtrlV();
        }

        _logger.LogInformation(
            "Secret {Mode} (restoreClipboard={RestoreClipboard})",
            requiresManualPaste ? "copied to clipboard for manual paste" : "pasted into foreground window",
            restoreClipboardAfter);

        if (restoreClipboardAfter)
        {
            var effectiveDelayMs = requiresManualPaste ? Math.Max(restoreDelayMs, MinManualPasteWindowMs) : restoreDelayMs;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Math.Max(effectiveDelayMs, 200)) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                RestoreClipboard(previousClipboard);
            };
            timer.Start();
        }

        return !requiresManualPaste;
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
