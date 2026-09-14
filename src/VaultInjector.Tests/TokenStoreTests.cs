using VaultInjector.Core.Services;

namespace VaultInjector.Tests;

public class TokenStoreTests
{
    [Fact]
    public void SaveLoadClear_RoundTrips()
    {
        ITokenStore store = new FakeTokenStore();

        Assert.False(store.HasToken);
        Assert.Null(store.LoadToken());

        store.SaveToken("s.abcdef123456");

        Assert.True(store.HasToken);
        Assert.Equal("s.abcdef123456", store.LoadToken());

        store.ClearToken();

        Assert.False(store.HasToken);
        Assert.Null(store.LoadToken());
    }
}
