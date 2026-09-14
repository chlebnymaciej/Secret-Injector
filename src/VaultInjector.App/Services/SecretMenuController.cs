using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using VaultInjector.App.Native;
using VaultInjector.Core.Models;

namespace VaultInjector.App.Services;

/// <summary>
/// Reacts to the global hotkey: captures whatever window currently has focus, pops the secret picker
/// at the cursor, and on selection fetches the value from Vault and pastes it back into that window.
/// </summary>
public sealed class SecretMenuController
{
    private readonly AppRuntime _runtime;
    private readonly ClipboardPasteService _pasteService;
    private readonly Action<string, ToolTipIcon> _notify;
    private readonly ILogger<SecretMenuController> _logger;

    public SecretMenuController(
        AppRuntime runtime,
        ClipboardPasteService pasteService,
        Action<string, ToolTipIcon> notify,
        ILogger<SecretMenuController> logger)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _pasteService = pasteService ?? throw new ArgumentNullException(nameof(pasteService));
        _notify = notify ?? throw new ArgumentNullException(nameof(notify));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void ShowMenu()
    {
        // Capture immediately - showing our own menu will itself steal the foreground.
        var targetWindow = NativeMethods.GetForegroundWindow();
        var config = _runtime.Config;

        if (config.Secrets.Count == 0)
        {
            _notify("No secrets configured yet. Open Settings to add some.", ToolTipIcon.Info);
            return;
        }

        if (!_runtime.TokenStore.HasToken)
        {
            _notify("Not logged in to Vault. Open Settings to log in.", ToolTipIcon.Warning);
            return;
        }

        NativeMethods.GetCursorPos(out var cursor);

        var menu = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = false
        };
        // Disposing synchronously here races with WinForms' own ModalMenuFilter, which keeps touching
        // this control's handle right after Closed fires (to process the activation change) and throws
        // ObjectDisposedException if we've already torn it down - crashing the whole process, since that
        // filter runs on a raw message hook outside the WPF dispatcher's exception handling. Deferring the
        // dispose to the next dispatcher pass lets that in-flight processing finish first.
        menu.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(menu.Dispose));

        foreach (var entry in config.Secrets)
        {
            var capturedEntry = entry;
            var item = new ToolStripMenuItem(entry.Alias);
            item.Click += (_, _) => _ = OnSecretSelectedAsync(capturedEntry, targetWindow);
            menu.Items.Add(item);
        }

        menu.Show(new System.Drawing.Point(cursor.X, cursor.Y));
    }

    private async Task OnSecretSelectedAsync(SecretEntry entry, IntPtr targetWindow)
    {
        _logger.LogInformation("User selected secret '{Alias}' ({RelativePath})", entry.Alias, entry.RelativePath);

        var token = _runtime.TokenStore.LoadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _notify("Not logged in to Vault. Open Settings to log in.", ToolTipIcon.Warning);
            return;
        }

        try
        {
            var value = await _runtime.VaultService
                .ResolveSecretValueAsync(_runtime.Config.Vault, token, entry)
                .ConfigureAwait(true);

            _pasteService.PasteIntoWindow(
                value,
                targetWindow,
                _runtime.Config.RestoreClipboardAfterPaste,
                _runtime.Config.ClipboardRestoreDelayMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch/paste secret '{Alias}'", entry.Alias);
            _notify($"Failed to fetch '{entry.Alias}': {ex.Message}", ToolTipIcon.Error);
        }
    }
}
