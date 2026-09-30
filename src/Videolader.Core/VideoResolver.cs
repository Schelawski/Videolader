namespace Videolader.Core;

/// <summary>
/// Turns the parsed input into the list of videos: expands playlists and channel tabs and looks up
/// titles. With an API key the YouTube Data API is used (fast, exact); without one, yt-dlp lists
/// playlists and oEmbed provides the titles of single videos. Videos occurring twice are listed once.
/// </summary>
public sealed class VideoResolver(HttpClient http, YtDlpClient? ytDlp, string? apiKey, string? cookiesBrowser, IProgress<string> log)
{
    private const int MaxParallelLookups = 4;

    private readonly YouTubeApiClient? _api = string.IsNullOrWhiteSpace(apiKey) ? null : new YouTubeApiClient(http, apiKey.Trim());
    private readonly OEmbedClient _oEmbed = new(http);

    public async Task<IReadOnlyList<VideoEntry>> ResolveAsync(IReadOnlyList<InputItem> items, CancellationToken ct = default)
    {
        var result = new List<VideoEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var singles = new List<VideoEntry>();
        var duplicates = 0;

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            switch (item.Kind)
            {
                case InputKind.Video:
                    if (seen.Add(item.Value))
                    {
                        var entry = new VideoEntry(item.Value);
                        result.Add(entry);
                        singles.Add(entry);
                    }
                    else
                    {
                        duplicates++;
                    }

                    break;

                default:
                    var list = await ListAsync(item, ct).ConfigureAwait(false);
                    if (list is null)
                        break;

                    var added = 0;
                    foreach (var entry in list.Entries)
                    {
                        if (seen.Add(entry.Id))
                        {
                            result.Add(entry);
                            added++;
                        }
                        else
                        {
                            duplicates++;
                        }
                    }

                    var name = list.Title ?? item.Value;
                    log.Report($"„{name}“: {list.Entries.Count} Videos" +
                               (added < list.Entries.Count ? $", davon {list.Entries.Count - added} schon in der Liste" : string.Empty) + ".");
                    break;
            }
        }

        if (singles.Count > 0)
            await FillSinglesAsync(singles, ct).ConfigureAwait(false);

        if (duplicates > 0)
            log.Report($"{duplicates} doppelte Einträge wurden zusammengefasst.");

        return result;
    }

    private async Task<VideoList?> ListAsync(InputItem item, CancellationToken ct)
    {
        if (item.Kind == InputKind.Playlist && _api is not null)
        {
            try
            {
                return await _api.GetPlaylistAsync(item.Value, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is YouTubeApiException or HttpRequestException)
            {
                log.Report($"YouTube-API: {ex.Message} – versuche es mit yt-dlp.");
            }
        }

        if (ytDlp is null)
        {
            log.Report($"Playlist {item.Value} kann nicht geladen werden: yt-dlp fehlt (oder API-Key eintragen).");
            return null;
        }

        try
        {
            return await ytDlp.ListAsync(item.Url, cookiesBrowser, ct).ConfigureAwait(false);
        }
        catch (YtDlpException ex)
        {
            log.Report($"{item.Original}: {ex.Message}");
            return null;
        }
    }

    private async Task FillSinglesAsync(List<VideoEntry> singles, CancellationToken ct)
    {
        if (_api is not null)
        {
            try
            {
                var found = await _api.GetVideosAsync(singles.Select(s => s.Id), ct).ConfigureAwait(false);
                foreach (var entry in singles)
                {
                    if (found.TryGetValue(entry.Id, out var video))
                    {
                        entry.Title = video.Title;
                        entry.Channel = video.Channel;
                        entry.Duration = video.Duration;
                    }
                    else
                    {
                        entry.Unavailable = true;
                    }
                }

                return;
            }
            catch (Exception ex) when (ex is YouTubeApiException or HttpRequestException)
            {
                log.Report($"YouTube-API: {ex.Message} – Titel werden ohne API ermittelt.");
            }
        }

        using var gate = new SemaphoreSlim(MaxParallelLookups);
        await Task.WhenAll(singles.Select(async entry =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var info = await _oEmbed.GetAsync(entry.Id, ct).ConfigureAwait(false);
                if (info is { } found)
                {
                    entry.Title = found.Title;
                    entry.Channel = found.Channel;
                }
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
    }
}
