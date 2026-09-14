using VaultInjector.Core.Models;
using VaultInjector.Core.Services;

namespace VaultInjector.Tests;

public class JsonConfigStoreTests : IDisposable
{
    private readonly string _tempFile;

    public JsonConfigStoreTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"vault-injector-tests-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
        {
            File.Delete(_tempFile);
        }
    }

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefaultConfig()
    {
        var store = new JsonConfigStore(_tempFile);

        var config = store.Load();

        Assert.NotNull(config);
        Assert.Empty(config.Secrets);
        Assert.Equal("Information", config.LogLevel);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsAllFields()
    {
        var store = new JsonConfigStore(_tempFile);
        var original = new AppConfig
        {
            StartWithWindows = true,
            RestoreClipboardAfterPaste = false,
            ClipboardRestoreDelayMs = 2500,
            LogLevel = "Debug",
            LogRetainedDays = 30,
            Vault = new VaultConnectionSettings
            {
                Address = "https://vault.internal:8200",
                Namespace = "team-a",
                MountPath = "kv",
                KvVersion = KvVersion.V1,
                BasePath = "myapp/prod",
                SkipTlsVerify = true
            },
            MenuHotkey = new HotkeyDefinition
            {
                Modifiers = HotkeyModifiers.Control | HotkeyModifiers.Shift,
                VirtualKeyCode = 0x56
            },
            Secrets = new List<SecretEntry>
            {
                new()
                {
                    Alias = "DB Password",
                    RelativePath = "db",
                    Mode = SecretFetchMode.SingleField,
                    FieldName = "password"
                },
                new()
                {
                    Alias = "Full API Config",
                    RelativePath = "api",
                    Mode = SecretFetchMode.WholeSecretAsJson
                }
            }
        };

        store.Save(original);
        var loaded = store.Load();

        Assert.Equal(original.StartWithWindows, loaded.StartWithWindows);
        Assert.Equal(original.RestoreClipboardAfterPaste, loaded.RestoreClipboardAfterPaste);
        Assert.Equal(original.ClipboardRestoreDelayMs, loaded.ClipboardRestoreDelayMs);
        Assert.Equal(original.LogLevel, loaded.LogLevel);
        Assert.Equal(original.Vault.Address, loaded.Vault.Address);
        Assert.Equal(original.Vault.Namespace, loaded.Vault.Namespace);
        Assert.Equal(original.Vault.KvVersion, loaded.Vault.KvVersion);
        Assert.Equal(original.Vault.SkipTlsVerify, loaded.Vault.SkipTlsVerify);
        Assert.Equal(original.MenuHotkey.Modifiers, loaded.MenuHotkey.Modifiers);
        Assert.Equal(original.MenuHotkey.VirtualKeyCode, loaded.MenuHotkey.VirtualKeyCode);
        Assert.Equal(2, loaded.Secrets.Count);
        Assert.Equal("DB Password", loaded.Secrets[0].Alias);
        Assert.Equal(SecretFetchMode.SingleField, loaded.Secrets[0].Mode);
        Assert.Equal("password", loaded.Secrets[0].FieldName);
        Assert.Equal(SecretFetchMode.WholeSecretAsJson, loaded.Secrets[1].Mode);
    }

    [Fact]
    public void Save_NeverWritesAToken()
    {
        var store = new JsonConfigStore(_tempFile);
        store.Save(new AppConfig());

        var json = File.ReadAllText(_tempFile);

        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }
}
