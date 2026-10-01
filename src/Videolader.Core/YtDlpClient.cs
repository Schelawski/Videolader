using System.Text.Json;

namespace Videolader.Core;

/// <summary>Result of listing a playlist or channel tab.</summary>
public sealed record VideoList(string? Id, string? Title, IReadOnlyList<VideoEntry> Entries);

public sealed class YtDlpException(string message) : Exception(message);

/// <summary>Calls yt-dlp.</summary>
public sealed class YtDlpClient
{
    private readonly ToolSet _tools;
    private readonly string _ytDlp;

    public YtDlpClient(ToolSet tools)
    {
        _tools = tools;
        _ytDlp = tools.YtDlp ?? throw new InvalidOperationException("yt-dlp wurde nicht gefunden.");
    }

    public async Task<string> GetVersionAsync(CancellationToken ct = default)
    {
        var (_, stdout, _) = await ProcessRunner.CaptureAsync(_ytDlp, ["--version"], _tools.CreateEnvironment(), ct).ConfigureAwait(false);
        return stdout.Trim();
    }

    /// <summary>Updates yt-dlp.exe in place (<c>yt-dlp -U</c>).</summary>
    public Task<int> UpdateAsync(Action<string> onLine, CancellationToken ct = default) =>
        ProcessRunner.RunAsync(_ytDlp, ["-U"], onLine, _tools.CreateEnvironment(), ct);

    public Task<int> RunAsync(IReadOnlyList<string> arguments, Action<string> onLine, CancellationToken ct = default) =>
        ProcessRunner.RunAsync(_ytDlp, arguments, onLine, _tools.CreateEnvironment(), ct);

    /// <summary>Lists the videos of a playlist or channel tab without downloading.</summary>
    public async Task<VideoList> ListAsync(string url, string? cookiesBrowser, string? contentLanguage = null, CancellationToken ct = default)
    {
        var (exitCode, stdout, stderr) = await ProcessRunner.CaptureAsync(
            _ytDlp, YtDlpCommandLine.BuildList(url, cookiesBrowser, contentLanguage),_tools.CreateEnvironment(), ct).ConfigureAwait(false);

        if (exitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            var error = stderr.Split('\n').Select(l => YtDlpOutput.TryGetError(l.Trim())).LastOrDefault(e => e is not null);
            if (error is null)
            {
                var text = stderr.Trim();
                error = text.Length > 0 ? text : $"yt-dlp beendet mit Code {exitCode}.";
            }

            throw new YtDlpException(error);
        }

        return ParseList(stdout);
    }

    /// <summary>Parses the output of <c>yt-dlp --flat-playlist -J</c>.</summary>
    internal static VideoList ParseList(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var listId = GetString(root, "id");
        var listTitle = GetString(root, "title");
        var entries = new List<VideoEntry>();

        if (root.TryGetProperty("entries", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in items.EnumerateArray())
            {
                index++;
                var id = GetString(item, "id");
                if (id is null || !InputParser.IsVideoId(id))
                    continue;

                var title = GetString(item, "title");
                entries.Add(new VideoEntry(id)
                {
                    Title = title,
                    Channel = GetString(item, "channel") ?? GetString(item, "uploader"),
                    Duration = GetSeconds(item, "duration"),
                    Unavailable = VideoEntry.IsUnavailableTitle(title) ||
                                  GetString(item, "availability") is "private" or "needs_auth" or "subscriber_only",
                    PlaylistId = listId,
                    PlaylistTitle = listTitle,
                    PlaylistIndex = GetInt(item, "playlist_index") ?? index,
                });
            }
        }
        else if (listId is not null && InputParser.IsVideoId(listId))
        {
            // A single video was passed.
            entries.Add(new VideoEntry(listId)
            {
                Title = listTitle,
                Channel = GetString(root, "channel") ?? GetString(root, "uploader"),
                Duration = GetSeconds(root, "duration"),
            });
            return new VideoList(null, null, entries);
        }

        return new VideoList(listId, listTitle, entries);
    }

    internal static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    internal static int? GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) ? i : null;

    internal static TimeSpan? GetSeconds(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? TimeSpan.FromSeconds(value.GetDouble())
            : null;
}
