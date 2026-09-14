using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using VaultInjector.App.Services;
using VaultInjector.Core.Models;
using ComboBox = System.Windows.Controls.ComboBox;
using ComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace VaultInjector.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppRuntime _runtime;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<SettingsWindow> _logger;
    private readonly ObservableCollection<SecretEntry> _secrets;

    public SettingsWindow(AppRuntime runtime, ILoggerFactory loggerFactory)
    {
        InitializeComponent();
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = _loggerFactory.CreateLogger<SettingsWindow>();

        var config = _runtime.Config;

        VaultAddressBox.Text = config.Vault.Address;
        NamespaceBox.Text = config.Vault.Namespace ?? string.Empty;
        MountPathBox.Text = config.Vault.MountPath;
        KvVersionCombo.SelectedIndex = config.Vault.KvVersion == KvVersion.V1 ? 1 : 0;
        BasePathBox.Text = config.Vault.BasePath;
        SkipTlsCheckBox.IsChecked = config.Vault.SkipTlsVerify;

        HotkeyRecorder.SetHotkey(config.MenuHotkey);

        StartWithWindowsCheckBox.IsChecked = config.StartWithWindows;
        RestoreClipboardCheckBox.IsChecked = config.RestoreClipboardAfterPaste;
        ClipboardDelayBox.Text = config.ClipboardRestoreDelayMs.ToString();
        LogRetainedDaysBox.Text = config.LogRetainedDays.ToString();
        SelectComboByContent(LogLevelCombo, config.LogLevel);

        _secrets = new ObservableCollection<SecretEntry>(config.Secrets.Select(s => s.Clone()));
        SecretsGrid.ItemsSource = _secrets;

        _ = RefreshLoginStatusAsync();
    }

    private static void SelectComboByContent(ComboBox combo, string content)
    {
        foreach (var obj in combo.Items)
        {
            if (obj is ComboBoxItem item && string.Equals(item.Content as string, content, StringComparison.OrdinalIgnoreCase))
            {
                combo.SelectedItem = item;
                return;
            }
        }

        combo.SelectedIndex = 2; // "Information"
    }

    private VaultConnectionSettings BuildVaultSettingsFromForm() => new()
    {
        Address = VaultAddressBox.Text.Trim(),
        Namespace = string.IsNullOrWhiteSpace(NamespaceBox.Text) ? null : NamespaceBox.Text.Trim(),
        MountPath = string.IsNullOrWhiteSpace(MountPathBox.Text) ? "secret" : MountPathBox.Text.Trim(),
        KvVersion = (KvVersionCombo.SelectedItem as ComboBoxItem)?.Tag as string == "V1" ? KvVersion.V1 : KvVersion.V2,
        BasePath = BasePathBox.Text.Trim(),
        SkipTlsVerify = SkipTlsCheckBox.IsChecked == true
    };

    private async Task RefreshLoginStatusAsync()
    {
        if (!_runtime.TokenStore.HasToken)
        {
            LoginStatusText.Text = "Not logged in.";
            return;
        }

        var token = _runtime.TokenStore.LoadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            LoginStatusText.Text = "Not logged in.";
            return;
        }

        LoginStatusText.Text = "Checking stored token...";
        var info = await _runtime.VaultService.ValidateTokenAsync(BuildVaultSettingsFromForm(), token);
        LoginStatusText.Text = info.IsValid
            ? $"Logged in as '{info.DisplayName}'. Policies: {string.Join(", ", info.Policies)}. Expires: {(info.ExpiresAt?.ToLocalTime().ToString("g") ?? "never")}."
            : $"Stored token is no longer valid: {info.Error}";
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Password;
        if (string.IsNullOrWhiteSpace(token))
        {
            LoginStatusText.Text = "Paste a Vault token first.";
            return;
        }

        LoginButton.IsEnabled = false;
        LoginStatusText.Text = "Logging in...";
        try
        {
            var info = await _runtime.VaultService.ValidateTokenAsync(BuildVaultSettingsFromForm(), token);
            if (info.IsValid)
            {
                _runtime.TokenStore.SaveToken(token);
                TokenBox.Clear();
                LoginStatusText.Text = $"Logged in as '{info.DisplayName}'. Policies: {string.Join(", ", info.Policies)}.";
                _logger.LogInformation("User logged in to Vault via Settings window");
            }
            else
            {
                LoginStatusText.Text = $"Login failed: {info.Error}";
            }
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        _runtime.TokenStore.ClearToken();
        LoginStatusText.Text = "Not logged in.";
    }

    private void AddSecretButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SecretEntryEditDialog(_runtime, _loggerFactory, null) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _secrets.Add(dialog.Result);
        }
    }

    private void EditSecretButton_Click(object sender, RoutedEventArgs e)
    {
        if (SecretsGrid.SelectedItem is not SecretEntry selected)
        {
            return;
        }

        var index = _secrets.IndexOf(selected);
        var dialog = new SecretEntryEditDialog(_runtime, _loggerFactory, selected) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _secrets[index] = dialog.Result;
        }
    }

    private void RemoveSecretButton_Click(object sender, RoutedEventArgs e)
    {
        if (SecretsGrid.SelectedItem is SecretEntry selected)
        {
            _secrets.Remove(selected);
        }
    }

    private void PreviewKeysButton_Click(object sender, RoutedEventArgs e)
    {
        var initialPath = (SecretsGrid.SelectedItem as SecretEntry)?.RelativePath;
        var dialog = new PreviewKeysDialog(_runtime, _loggerFactory, initialPath) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SecretEntry? lastAdded = null;
        var skipped = 0;
        foreach (var key in dialog.SelectedKeys)
        {
            var alreadyPresent = _secrets.Any(s =>
                s.Mode == SecretFetchMode.SingleField &&
                string.Equals(s.RelativePath, key.RelativePath, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.FieldName, key.FieldName, StringComparison.OrdinalIgnoreCase));

            if (alreadyPresent)
            {
                skipped++;
                continue;
            }

            var leaf = key.RelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? key.RelativePath;
            lastAdded = new SecretEntry
            {
                Alias = $"{leaf}/{key.FieldName}",
                RelativePath = key.RelativePath,
                Mode = SecretFetchMode.SingleField,
                FieldName = key.FieldName
            };
            _secrets.Add(lastAdded);
        }

        if (lastAdded is not null)
        {
            SecretsGrid.SelectedItem = lastAdded;
        }

        if (skipped > 0)
        {
            StatusText.Text = $"Added {dialog.SelectedKeys.Count - skipped} key(s) to Quick Insert ({skipped} already present, skipped).";
        }
        else if (dialog.SelectedKeys.Count > 0)
        {
            StatusText.Text = $"Added {dialog.SelectedKeys.Count} key(s) to Quick Insert.";
        }
    }

    private void MoveUpButton_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);

    private void MoveDownButton_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int offset)
    {
        if (SecretsGrid.SelectedItem is not SecretEntry selected)
        {
            return;
        }

        var index = _secrets.IndexOf(selected);
        var newIndex = index + offset;
        if (newIndex < 0 || newIndex >= _secrets.Count)
        {
            return;
        }

        _secrets.Move(index, newIndex);
        SecretsGrid.SelectedItem = selected;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(VaultAddressBox.Text))
        {
            StatusText.Text = "Vault address is required.";
            return;
        }

        if (!int.TryParse(ClipboardDelayBox.Text, out var delayMs) || delayMs < 0)
        {
            StatusText.Text = "Clipboard restore delay must be a non-negative number.";
            return;
        }

        if (!int.TryParse(LogRetainedDaysBox.Text, out var retainedDays) || retainedDays < 1)
        {
            StatusText.Text = "Retained days must be at least 1.";
            return;
        }

        var config = new AppConfig
        {
            Vault = BuildVaultSettingsFromForm(),
            MenuHotkey = HotkeyRecorder.Hotkey,
            Secrets = _secrets.ToList(),
            StartWithWindows = StartWithWindowsCheckBox.IsChecked == true,
            RestoreClipboardAfterPaste = RestoreClipboardCheckBox.IsChecked == true,
            ClipboardRestoreDelayMs = delayMs,
            LogLevel = (LogLevelCombo.SelectedItem as ComboBoxItem)?.Content as string ?? "Information",
            LogRetainedDays = retainedDays
        };

        _runtime.SaveConfig(config);
        _runtime.AutoStartService.SetEnabled(config.StartWithWindows);

        StatusText.Text = $"Saved at {DateTime.Now:T}.";
        _logger.LogInformation("Settings saved via UI");
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void OpenLogsButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_runtime.LogsDirectory);
            Process.Start(new ProcessStartInfo { FileName = _runtime.LogsDirectory, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open logs folder from settings window");
        }
    }
}
