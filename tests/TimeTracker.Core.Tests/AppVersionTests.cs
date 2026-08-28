using TimeTracker.Core;

namespace TimeTracker.Core.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("v2.1.1", 2, 1, 1)]
    [InlineData("2.0.0", 2, 0, 0)]
    [InlineData("v1.2.3-beta", 1, 2, 3)]
    public void TryParseTag_parses_semver_tags(string tag, int major, int minor, int build)
    {
        Assert.True(AppVersion.TryParseTag(tag, out var version));
        Assert.Equal(new Version(major, minor, build), version);
    }

    [Fact]
    public void TryParseTag_rejects_invalid()
    {
        Assert.False(AppVersion.TryParseTag("latest", out _));
    }
}
