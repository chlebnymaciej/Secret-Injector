using VaultInjector.Core.Models;

namespace VaultInjector.Core.Services;

public interface IConfigStore
{
    string ConfigFilePath { get; }
    AppConfig Load();
    void Save(AppConfig config);
}
