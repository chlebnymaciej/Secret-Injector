using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.Logging;
using VaultInjector.App.Services;
using VaultInjector.Core.Services;

namespace VaultInjector.App.Views;

/// <summary>
/// Recursively walks every sub-path under a starting Vault path and previews just the field keys found
/// (no values fetched beyond what's needed to list them), letting the user pick a subset to quickly add
/// as new Quick Insert (Secrets Menu) entries.
/// </summary>
public partial class PreviewKeysDialog : Window
{
    private const int MaxDepth = 6;
    private const int MaxItems = 500;

    private readonly AppRuntime _runtime;
    private readonly ILogger<PreviewKeysDialog> _logger;
    private readonly ObservableCollection<KeyItem> _keys = new();

    /// <summary>The keys the user checked and chose to add, populated when the dialog closes via "Add Selected".</summary>
    public IReadOnlyList<SelectedSecretKey> SelectedKeys { get; private set; } = Array.Empty<SelectedSecretKey>();

    public PreviewKeysDialog(AppRuntime runtime, ILoggerFactory loggerFactory, string? initialPath)
    {
        InitializeComponent();
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _logger = (loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory))).CreateLogger<PreviewKeysDialog>();

        Icon = TrayIconRenderer.RenderKeyIconImageSource(32);
        PathBox.Text = initialPath ?? string.Empty;
        KeysList.ItemsSource = _keys;
    }

    private async void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var token = _runtime.TokenStore.LoadToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            StatusText.Text = "Not logged in to Vault. Log in from the Vault Connection tab first.";
            return;
        }

        var startPath = PathBox.Text.Trim('/');

        LoadButton.IsEnabled = false;
        StatusText.Text = "Loading...";
        _keys.Clear();
        var warnings = new List<string>();
        try
        {
            await RecurseAsync(token, startPath, 0, warnings);

            StatusText.Text = _keys.Count > 0
                ? $"Found {_keys.Count} key(s) across {_keys.Select(k => k.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count()} secret(s)."
                : "No keys found under this path.";

            if (warnings.Count > 0)
            {
                var shown = warnings.Take(3);
                StatusText.Text += " " + string.Join(" ", shown);
                if (warnings.Count > 3)
                {
                    StatusText.Text += $" (+{warnings.Count - 3} more issue(s), see logs)";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Recursive key preview failed for starting path '{StartPath}'", startPath);
            StatusText.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    /// <summary>Recursively lists <paramref name="path"/>; returns false once the overall item cap has been hit.</summary>
    private async Task<bool> RecurseAsync(string token, string path, int depth, List<string> warnings)
    {
        if (_keys.Count >= MaxItems)
        {
            return false;
        }

        if (depth > MaxDepth)
        {
            warnings.Add($"Stopped recursing at '{path}' (max depth {MaxDepth} reached).");
            return true;
        }

        IReadOnlyList<string> children;
        try
        {
            children = await _runtime.VaultService.ListSecretPathsAsync(_runtime.Config.Vault, token, path);
        }
        catch (VaultServiceException ex)
        {
            // Not listable (e.g. no LIST permission, or it's a leaf secret) - try reading it directly instead.
            await AddLeafAsync(token, path, warnings, ex);
            return true;
        }

        if (children.Count == 0)
        {
            // Listable but empty usually means this path is itself a leaf secret.
            await AddLeafAsync(token, path, warnings, null);
            return true;
        }

        foreach (var child in children)
        {
            if (_keys.Count >= MaxItems)
            {
                warnings.Add($"Stopped after {MaxItems} keys - narrow the starting path to see more.");
                return false;
            }

            var trimmedChild = child.Trim('/');
            var childPath = string.IsNullOrEmpty(path) ? trimmedChild : $"{path}/{trimmedChild}";

            var keepGoing = child.EndsWith('/')
                ? await RecurseAsync(token, childPath, depth + 1, warnings)
                : await AddLeafAsync(token, childPath, warnings, null);

            if (!keepGoing)
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> AddLeafAsync(string token, string path, List<string> warnings, Exception? listFailure)
    {
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        try
        {
            var data = await _runtime.VaultService.ReadSecretDataAsync(_runtime.Config.Vault, token, path);
            foreach (var field in data.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (_keys.Count >= MaxItems)
                {
                    break;
                }

                _keys.Add(new KeyItem(path, field));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read secret at '{RelativePath}' while previewing keys", path);
            warnings.Add($"'{path}': {(listFailure is null ? ex.Message : listFailure.Message)}.");
        }

        return true;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _keys)
        {
            item.IsSelected = true;
        }
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in _keys)
        {
            item.IsSelected = false;
        }
    }

    private void AddSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = _keys.Where(k => k.IsSelected)
            .Select(k => new SelectedSecretKey(k.RelativePath, k.FieldName))
            .ToList();

        if (selected.Count == 0)
        {
            StatusText.Text = "Check at least one key first.";
            return;
        }

        SelectedKeys = selected;
        DialogResult = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    private sealed class KeyItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public KeyItem(string relativePath, string fieldName)
        {
            RelativePath = relativePath;
            FieldName = fieldName;
        }

        public string RelativePath { get; }

        public string FieldName { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}

/// <summary>One key the user picked in <see cref="PreviewKeysDialog"/> to add as a Quick Insert entry.</summary>
public sealed record SelectedSecretKey(string RelativePath, string FieldName);
