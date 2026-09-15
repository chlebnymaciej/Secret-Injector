using System.Windows;
using Microsoft.Extensions.Logging;
using VaultInjector.App.Services;
using VaultInjector.Core.Models;
using MessageBox = System.Windows.MessageBox;

namespace VaultInjector.App.Views;

public partial class SecretEntryEditDialog : Window
{
    private readonly AppRuntime _runtime;
    private readonly ILogger<SecretEntryEditDialog> _logger;

    public SecretEntry Result { get; private set; } = new();

    public SecretEntryEditDialog(AppRuntime runtime, ILoggerFactory loggerFactory, SecretEntry? existing)
    {
        InitializeComponent();
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<SecretEntryEditDialog>();

        Icon = TrayIconRenderer.RenderKeyIconImageSource(32);

        if (existing is not null)
        {
            Result = existing.Clone();
            AliasBox.Text = Result.Alias;
            RelativePathBox.Text = Result.RelativePath;
            WholeJsonRadio.IsChecked = Result.Mode == SecretFetchMode.WholeSecretAsJson;
            SingleFieldRadio.IsChecked = Result.Mode == SecretFetchMode.SingleField;
            FieldNameCombo.Text = Result.FieldName ?? string.Empty;
        }

        UpdateFieldEnablement();
    }

    private void ModeRadio_Checked(object sender, RoutedEventArgs e) => UpdateFieldEnablement();

    private void UpdateFieldEnablement()
    {
        // SingleFieldRadio's IsChecked="True" in XAML fires Checked during InitializeComponent,
        // before FieldNameCombo (declared later in the tree) has been assigned to its field.
        if (FieldNameCombo is null)
        {
            return;
        }

        FieldNameCombo.IsEnabled = SingleFieldRadio.IsChecked == true;
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RelativePathBox.Text))
        {
            TestResultText.Text = "Enter a Vault path first.";
            return;
        }

        var token = _runtime.TokenStore.LoadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            TestResultText.Text = "Not logged in to Vault. Log in from the Vault Connection tab first.";
            return;
        }

        TestButton.IsEnabled = false;
        TestResultText.Text = "Testing...";
        try
        {
            var data = await _runtime.VaultService.ReadSecretDataAsync(_runtime.Config.Vault, token, RelativePathBox.Text.Trim());
            var fields = data.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

            FieldNameCombo.ItemsSource = fields;
            TestResultText.Text = fields.Count > 0
                ? $"Found {fields.Count} field(s): {string.Join(", ", fields)}"
                : "Secret exists but has no fields.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Test/Load Fields failed for path '{RelativePath}'", RelativePathBox.Text.Trim());
            TestResultText.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(AliasBox.Text) || string.IsNullOrWhiteSpace(RelativePathBox.Text))
        {
            MessageBox.Show(this, "Alias and Vault path are required.", "Vault Injector", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mode = WholeJsonRadio.IsChecked == true ? SecretFetchMode.WholeSecretAsJson : SecretFetchMode.SingleField;
        if (mode == SecretFetchMode.SingleField && string.IsNullOrWhiteSpace(FieldNameCombo.Text))
        {
            MessageBox.Show(this, "Enter or select a field name for single-field mode.", "Vault Injector", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result.Alias = AliasBox.Text.Trim();
        Result.RelativePath = RelativePathBox.Text.Trim();
        Result.Mode = mode;
        Result.FieldName = mode == SecretFetchMode.SingleField ? FieldNameCombo.Text.Trim() : null;

        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
