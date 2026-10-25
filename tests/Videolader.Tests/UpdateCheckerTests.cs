using System.Net;
using System.Text;
using Videolader.Core;

namespace Videolader.Tests;

public class UpdateCheckerTests
{
    private const string Release130 = """
        { "tag_name": "v1.3.0", "html_url": "https://github.com/Schelawski/Videolader/releases/tag/v1.3.0", "prerelease": false }
        """;

    [Fact]
    public void LatestReleaseIsParsed()
    {
        Assert.Equal(
            new ReleaseInfo("1.3.0", new Uri("https://github.com/Schelawski/Videolader/releases/tag/v1.3.0")),
            UpdateChecker.ParseLatestRelease(Release130));
    }

    [Theory]
    [InlineData("""{ "tag_name": "nightly" }""")]
    [InlineData("""{ "html_url": "https://github.com/x" }""")]
    [InlineData("[]")]
    public void ReleasesWithoutAVersionTagAreIgnored(string json)
    {
        Assert.Null(UpdateChecker.ParseLatestRelease(json));
    }

    [Fact]
    public void ForeignLinksAreReplacedByTheReleasesPage()
    {
        var release = UpdateChecker.ParseLatestRelease("""{ "tag_name": "v2.0.0", "html_url": "https://evil.example/download" }""");

        Assert.Equal(new Uri(UpdateChecker.ReleasesPageUrl), release!.PageUrl);
    }

    [Fact]
    public async Task NewerReleaseIsFound()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeGitHub(HttpStatusCode.OK, Release130)));

        Assert.Equal("1.3.0", (await checker.FindNewerReleaseAsync("1.2.0", CancellationToken.None))?.Version);
        Assert.Null(await checker.FindNewerReleaseAsync("1.3.0", CancellationToken.None));
    }

    [Fact]
    public async Task NothingReleasedYetIsAnAnswer()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeGitHub(HttpStatusCode.NotFound, "{}")));

        Assert.Equal((true, (ReleaseInfo?)null), await checker.FetchLatestReleaseAsync(CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.OK, "<html>not json</html>")]
    public async Task UnreachableOrBrokenGitHubIsNoAnswer(HttpStatusCode status, string body)
    {
        var checker = new UpdateChecker(new HttpClient(new FakeGitHub(status, body)));

        // Not "nothing released": the check is repeated on the next start.
        Assert.Equal((false, (ReleaseInfo?)null), await checker.FetchLatestReleaseAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OfflineIsNoAnswer()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeGitHub(null, "")));

        Assert.False((await checker.FetchLatestReleaseAsync(CancellationToken.None)).Reached);
    }

    [Fact]
    public void UpdateCheckIsOnByDefault()
    {
        Assert.True(new AppSettings().CheckForUpdates);
    }

    /// <summary>Answers every request with the same response; <c>null</c> status simulates "no internet".</summary>
    private sealed class FakeGitHub(HttpStatusCode? status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(UpdateChecker.LatestReleaseApiUrl, request.RequestUri!.ToString());
            return status is { } code
                ? Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") })
                : throw new HttpRequestException("No internet");
        }
    }
}
