using VaultInjector.Core.Models;
using VaultInjector.Core.Services;

namespace VaultInjector.Tests;

public class VaultPathBuilderTests
{
    [Fact]
    public void BuildLogicalPath_CombinesBasePathAndRelativePath()
    {
        var settings = new VaultConnectionSettings { BasePath = "myapp/prod" };

        var result = VaultPathBuilder.BuildLogicalPath(settings, "db/credentials");

        Assert.Equal("myapp/prod/db/credentials", result);
    }

    [Fact]
    public void BuildLogicalPath_TrimsSlashes()
    {
        var settings = new VaultConnectionSettings { BasePath = "/myapp/prod/" };

        var result = VaultPathBuilder.BuildLogicalPath(settings, "/db/credentials/");

        Assert.Equal("myapp/prod/db/credentials", result);
    }

    [Fact]
    public void BuildLogicalPath_EmptyBasePath_ReturnsRelativePathOnly()
    {
        var settings = new VaultConnectionSettings { BasePath = "" };

        var result = VaultPathBuilder.BuildLogicalPath(settings, "db/credentials");

        Assert.Equal("db/credentials", result);
    }

    [Fact]
    public void BuildLogicalPath_EmptyRelativePath_Throws()
    {
        var settings = new VaultConnectionSettings { BasePath = "myapp" };

        Assert.Throws<ArgumentException>(() => VaultPathBuilder.BuildLogicalPath(settings, " "));
    }

    [Fact]
    public void BuildLogicalPath_NullSettings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => VaultPathBuilder.BuildLogicalPath(null!, "db"));
    }
}
