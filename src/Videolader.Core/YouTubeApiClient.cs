using System.Net;
using System.Text.Json;
using System.Xml;

namespace Videolader.Core;

public sealed class YouTubeApiException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;
}

/// <summary>
/// Minimal client for the YouTube Data API v3. Only reads public data (playlists, titles, durations);
/// the API cannot download videos. One page of 50 entries costs 1 quota unit.
/// </summary>
public sealed class YouTubeApiClient(HttpClient http, string apiKey)
{
    private const string BaseUrl = "https://www.googleapis.com/youtube/v3/";
    private const int PageSize = 50;

    /// <summary>Title and all videos of a playlist, in playlist order, including durations.</summary>
    public async Task<VideoList> GetPlaylistAsync(string playlistId, CancellationToken ct = default)
    {
        string? title = null;
        using (var info = await GetAsync($"playlists?part=snippet&id={Uri.EscapeDataString(playlistId)}", ct).ConfigureAwait(false))
        {
            foreach (var item in Items(info.RootElement))
                title = item.GetProperty("snippet").GetProperty("title").GetString();
        }

        var entries = new List<VideoEntry>();
        string? pageToken = null;
        do
        {
            var query = $"playlistItems?part=snippet,contentDetails,status&maxResults={PageSize}&playlistId={Uri.EscapeDataString(playlistId)}";
            if (pageToken is not null)
                query += "&pageToken=" + Uri.EscapeDataString(pageToken);

            using var page = await GetAsync(query, ct).ConfigureAwait(false);
            foreach (var item in Items(page.RootElement))
            {
                var snippet = item.GetProperty("snippet");
                var videoId = item.TryGetProperty("contentDetails", out var details)
                    ? YtDlpClient.GetString(details, "videoId")
                    : null;
                videoId ??= snippet.TryGetProperty("resourceId", out var resource) ? YtDlpClient.GetString(resource, "videoId") : null;
                if (videoId is null)
                    continue;

                var itemTitle = YtDlpClient.GetString(snippet, "title");
                var privacy = item.TryGetProperty("status", out var status) ? YtDlpClient.GetString(status, "privacyStatus") : null;
                entries.Add(new VideoEntry(videoId)
                {
                    Title = itemTitle,
                    Channel = YtDlpClient.GetString(snippet, "videoOwnerChannelTitle"),
                    Unavailable = VideoEntry.IsUnavailableTitle(itemTitle) || privacy is "private" or "privacyStatusUnspecified",
                    PlaylistId = playlistId,
                    PlaylistTitle = title,
                    PlaylistIndex = (YtDlpClient.GetInt(snippet, "position") ?? entries.Count) + 1,
                });
            }

            pageToken = YtDlpClient.GetString(page.RootElement, "nextPageToken");
        }
        while (pageToken is not null);

        // Durations (and a second check that the video is really available).
        var details2 = await GetVideosAsync(entries.Where(e => !e.Unavailable).Select(e => e.Id), ct).ConfigureAwait(false);
        foreach (var entry in entries.Where(e => !e.Unavailable))
        {
            if (details2.TryGetValue(entry.Id, out var video))
            {
                entry.Duration = video.Duration;
                entry.Channel ??= video.Channel;
            }
            else
            {
                entry.Unavailable = true;
            }
        }

        return new VideoList(playlistId, title, entries);
    }

    /// <summary>Title, channel and duration of videos. Missing videos (private, deleted) are not in the result.</summary>
    public async Task<Dictionary<string, VideoEntry>> GetVideosAsync(IEnumerable<string> ids, CancellationToken ct = default)
    {
        var result = new Dictionary<string, VideoEntry>(StringComparer.Ordinal);
        foreach (var chunk in ids.Distinct(StringComparer.Ordinal).Chunk(PageSize))
        {
            var query = "videos?part=snippet,contentDetails&maxResults=50&id=" + Uri.EscapeDataString(string.Join(',', chunk));
            using var page = await GetAsync(query, ct).ConfigureAwait(false);
            foreach (var item in Items(page.RootElement))
            {
                var id = YtDlpClient.GetString(item, "id");
                if (id is null)
                    continue;

                var snippet = item.GetProperty("snippet");
                var duration = item.TryGetProperty("contentDetails", out var details) ? YtDlpClient.GetString(details, "duration") : null;
                result[id] = new VideoEntry(id)
                {
                    Title = YtDlpClient.GetString(snippet, "title"),
                    Channel = YtDlpClient.GetString(snippet, "channelTitle"),
                    Duration = ParseDuration(duration),
                };
            }
        }

        return result;
    }

    /// <summary>Parses ISO 8601 durations like "PT1H2M3S". Live streams report "P0D".</summary>
    internal static TimeSpan? ParseDuration(string? iso)
    {
        if (string.IsNullOrEmpty(iso))
            return null;
        try
        {
            var value = XmlConvert.ToTimeSpan(iso);
            return value == TimeSpan.Zero ? null : value;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IEnumerable<JsonElement> Items(JsonElement root) =>
        root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array
            ? items.EnumerateArray()
            : [];

    private async Task<JsonDocument> GetAsync(string pathAndQuery, CancellationToken ct)
    {
        var url = BaseUrl + pathAndQuery + "&key=" + Uri.EscapeDataString(apiKey);
        using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new YouTubeApiException(ReadErrorMessage(body) ?? $"HTTP {(int)response.StatusCode}", response.StatusCode);

        return JsonDocument.Parse(body);
    }

    internal static string? ReadErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                ? YtDlpClient.GetString(error, "message")
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// YouTube's oEmbed endpoint: title and channel of a single video, without API key.
/// </summary>
public sealed class OEmbedClient(HttpClient http)
{
    /// <summary>Returns (title, channel), or null if YouTube does not answer (private, embedding disabled, …).</summary>
    public async Task<(string Title, string? Channel)?> GetAsync(string videoId, CancellationToken ct = default)
    {
        var url = "https://www.youtube.com/oembed?format=json&url=" + Uri.EscapeDataString(YouTubeUrls.Video(videoId));
        try
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var title = YtDlpClient.GetString(document.RootElement, "title");
            return title is null ? null : (title, YtDlpClient.GetString(document.RootElement, "author_name"));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }
}
