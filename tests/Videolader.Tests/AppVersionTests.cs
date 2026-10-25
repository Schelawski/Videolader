using Videolader.Core;

namespace Videolader.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3, null)]
    [InlineData("v1.2.3", 1, 2, 3, null)]
    [InlineData("1.3.0-beta.1", 1, 3, 0, "beta.1")]
    [InlineData("v10.0.12-rc", 10, 0, 12, "rc")]
    public void VersionsAndTagsAreParsed(string text, int major, int minor, int patch, string? pre)
    {
        var parsed = AppVersion.Parse(text);

        Assert.NotNull(parsed);
        Assert.Equal(new Version(major, minor, patch), parsed.Value.Number);
        Assert.Equal(pre, parsed.Value.PreRelease);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1.2")]
    [InlineData("latest")]
    [InlineData("v1.2.3.4")]
    public void InvalidVersionsAreRejected(string? text)
    {
        Assert.Null(AppVersion.Parse(text));
    }

    [Theory]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("1.10.0", "1.9.0", true)]     // numeric, not text comparison
    [InlineData("1.3.0", "1.3.0-beta.1", true)] // the final version after its pre-release
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.1", false)]
    [InlineData("1.3.0-beta.2", "1.3.0", false)]
    [InlineData("garbage", "1.0.0", false)]
    public void NewerVersionsAreRecognized(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, AppVersion.IsNewer(candidate, current));
    }

    [Fact]
    public void CurrentVersionHasNoCommitSuffix()
    {
        Assert.NotNull(AppVersion.Parse(AppVersion.Current));
        Assert.DoesNotContain("+", AppVersion.Current);
    }

    [Fact]
    public void VersionOptionNeedsNoLink()
    {
        var result = CliArguments.Parse(["--version"]);

        Assert.Null(result.Error);
        Assert.True(result.Options!.ShowVersion);
    }
}
