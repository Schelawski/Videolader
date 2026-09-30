using System.Text.RegularExpressions;
using System.Web;

namespace Videolader.Core;

public enum InputKind
{
    /// <summary>A single video, <see cref="InputItem.Value"/> is the 11-character video ID.</summary>
    Video,

    /// <summary>A playlist, <see cref="InputItem.Value"/> is the playlist ID.</summary>
    Playlist,

    /// <summary>Any other YouTube URL yt-dlp can list (e.g. a channel's video tab).</summary>
    Url,
}

/// <param name="Kind">What the input refers to.</param>
/// <param name="Value">Video ID, playlist ID or URL.</param>
/// <param name="Original">The text as typed by the user.</param>
public sealed record InputItem(InputKind Kind, string Value, string Original)
{
    /// <summary>URL that yt-dlp understands for this item.</summary>
    public string Url => Kind switch
    {
        InputKind.Video => YouTubeUrls.Video(Value),
        InputKind.Playlist => YouTubeUrls.Playlist(Value),
        _ => Value,
    };
}

public sealed record ParsedInput(IReadOnlyList<InputItem> Items, IReadOnlyList<string> Invalid);

public static class YouTubeUrls
{
    public static string Video(string id) => "https://www.youtube.com/watch?v=" + id;

    public static string Playlist(string id) => "https://www.youtube.com/playlist?list=" + id;
}

/// <summary>
/// Turns the text of the input box (links or IDs, one or more per line) into <see cref="InputItem"/>s.
/// </summary>
public static partial class InputParser
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex VideoIdRegex();

    // PL = normal playlist, UU/UULF/UUSH = channel uploads, OL = album, FL/UL/PU = older kinds.
    [GeneratedRegex("^(PL|UU|OL|FL|UL|PU)[A-Za-z0-9_-]{10,}$")]
    private static partial Regex PlaylistIdRegex();

    private static readonly string[] VideoPathPrefixes = ["shorts", "live", "embed", "v", "e"];
    private static readonly string[] ChannelPathPrefixes = ["channel", "c", "user"];

    public static bool IsVideoId(string text) => VideoIdRegex().IsMatch(text);

    public static bool IsPlaylistId(string text) => PlaylistIdRegex().IsMatch(text);

    public static ParsedInput Parse(string? text)
    {
        var items = new List<InputItem>();
        var invalid = new List<string>();
        var seen = new HashSet<(InputKind, string)>();

        var tokens = (text ?? string.Empty)
            .Split(['\r', '\n', ' ', '\t', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var token in tokens)
        {
            var item = ParseToken(token);
            if (item is null)
                invalid.Add(token);
            else if (seen.Add((item.Kind, item.Value)))
                items.Add(item);
        }

        return new ParsedInput(items, invalid);
    }

    /// <summary>Parses one link or ID. Returns null if it is not recognized.</summary>
    public static InputItem? ParseToken(string token)
    {
        var text = token.Trim().Trim('"', '\'', '<', '>');
        if (text.Length == 0)
            return null;

        if (IsVideoId(text))
            return new InputItem(InputKind.Video, text, token);
        if (IsPlaylistId(text))
            return new InputItem(InputKind.Playlist, text, token);

        return ParseUrl(text, token);
    }

    private static InputItem? ParseUrl(string text, string original)
    {
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            if (!text.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) &&
                !text.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
                return null;
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return null;

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (host is "youtu.be" or "www.youtu.be")
        {
            return segments.Length > 0 && IsVideoId(segments[0])
                ? new InputItem(InputKind.Video, segments[0], original)
                : null;
        }

        if (!(host == "youtube.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) ||
              host == "youtube-nocookie.com" || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal)))
            return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        var list = query["list"];
        var videoId = query["v"];

        // Mixes ("RD…") are endless auto-generated lists, "WL"/"LL" are private: take the video instead.
        if (!string.IsNullOrEmpty(list) && IsDownloadablePlaylist(list))
            return new InputItem(InputKind.Playlist, list, original);

        if (!string.IsNullOrEmpty(videoId) && IsVideoId(videoId))
            return new InputItem(InputKind.Video, videoId, original);

        if (segments.Length >= 2 &&
            VideoPathPrefixes.Contains(segments[0], StringComparer.OrdinalIgnoreCase) &&
            IsVideoId(segments[1]))
            return new InputItem(InputKind.Video, segments[1], original);

        if (segments.Length == 0 || segments[0].Equals("watch", StringComparison.OrdinalIgnoreCase) ||
            segments[0].Equals("playlist", StringComparison.OrdinalIgnoreCase))
            return null;

        // Channel links: without a tab yt-dlp would list the tabs instead of the videos.
        var isHandle = segments[0].StartsWith('@');
        var isChannel = isHandle || ChannelPathPrefixes.Contains(segments[0], StringComparer.OrdinalIgnoreCase);
        if (isChannel)
        {
            var tabIndex = isHandle ? 1 : 2;
            if (!isHandle && segments.Length < 2)
                return null;

            var baseSegments = segments.Take(tabIndex);
            var tab = segments.Length > tabIndex ? segments[tabIndex] : "videos";
            var url = "https://www.youtube.com/" + string.Join('/', baseSegments) + "/" + tab;
            return new InputItem(InputKind.Url, url, original);
        }

        return null;
    }

    private static bool IsDownloadablePlaylist(string list) =>
        !list.StartsWith("RD", StringComparison.Ordinal) &&
        list is not ("WL" or "LL" or "LM");
}
