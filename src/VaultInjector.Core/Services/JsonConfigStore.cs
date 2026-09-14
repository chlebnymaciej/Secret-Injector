using System.Text.Json;
using System.Text.Json.Serialization;
using VaultInjector.Core.Models;

namespace VaultInjector.Core.Services;

/// <summary>Reads/writes <see cref="AppConfig"/> as indented JSON. Never touches the Vault token.</summary>
public sealed class JsonConfigStore : IConfigStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string ConfigFilePath { get; }

    public JsonConfigStore(string configFilePath)
    {
        ConfigFilePath = configFilePath ?? throw new ArgumentNullException(nameof(configFilePath));
    }

    public AppConfig Load()
    {
        if (!File.Exists(ConfigFilePath))
        {
            return new AppConfig();
        }

        var json = File.ReadAllText(ConfigFilePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppConfig();
        }

        return JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions) ?? new AppConfig();
    }

    public void Save(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var directory = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(config, SerializerOptions);

        // Write-then-move so a crash or power loss mid-write can never leave a truncated config file behind.
        var tempFile = ConfigFilePath + ".tmp";
        File.WriteAllText(tempFile, json);
        File.Copy(tempFile, ConfigFilePath, overwrite: true);
        File.Delete(tempFile);
    }
}
