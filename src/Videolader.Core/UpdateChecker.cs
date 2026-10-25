using System.Net;
using System.Text.Json;

namespace Videolader.Core;

/// <summary>A published version of Videolader.</summary>
/// <param name="Version">E.g. "1.3.0".</param>
/// <param name="PageUrl">The release page with the download.</param>
public sealed record ReleaseInfo(string Version, Uri PageUrl);

/// <summary>
/// Asks GitHub for the newest published version of Videolader. Only the version number is requested;
/// nothing about the user, the computer or the videos is sent. Can be switched off in the settings.
/// </summary>
public sealed class UpdateChecker(HttpClient http)
{
    /// <summary>The newest final release (GitHub leaves out pre-releases and drafts here).</summary>
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/Schelawski/Videolader/releases/latest";

    /// <summary>The page every "new version" hint links to.</summary>
    public const string ReleasesPageUrl = "https://github.com/Schelawski/Videolader/releases/latest";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <returns>The newer release, or <c>null</c> if there is none or GitHub cannot be reached.</returns>
    public async Task<ReleaseInfo?> FindNewerReleaseAsync(string currentVersion, CancellationToken cancellationToken) =>
        (await FetchLatestReleaseAsync(cancellationToken).ConfigureAwait(false)) is { Reached: true, Latest: { } latest }
        && AppVersion.IsNewer(latest.Version, currentVersion)
            ? latest
            : null;

    /// <returns>
    /// <c>Reached</c>: GitHub answered (otherwise try again later); <c>Latest</c>: the newest final release, or
    /// <c>null</c> if nothing is released yet.
    /// </returns>
    public async Task<(bool Reached, ReleaseInfo? Latest)> FetchLatestReleaseAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (true, null); // nothing released yet
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);
            return (true, ParseLatestRelease(json));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return (false, null); // offline, blocked or slow: the check is only a convenience
        }
    }

    /// <summary>Reads <c>tag_name</c> and <c>html_url</c> of a GitHub release.</summary>
    internal static ReleaseInfo? ParseLatestRelease(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("tag_name", out var tag)
            || tag.GetString() is not { } tagName
            || AppVersion.Parse(tagName) is null)
        {
            return null;
        }

        var page = root.TryGetProperty("html_url", out var url) && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var parsed)
            && parsed.Scheme == Uri.UriSchemeHttps && parsed.Host == "github.com"
            ? parsed
            : new Uri(ReleasesPageUrl);

        return new ReleaseInfo(tagName.TrimStart('v'), page);
    }
}
