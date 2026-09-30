using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Videolader.Core;

/// <summary>Contents of <c>&lt;ID&gt;.info.json</c> next to the video.</summary>
public sealed record VideoInfo
{
    public required string Id { get; init; }
    public string? Title { get; init; }
    public required string Url { get; init; }
    public string? Channel { get; init; }
    public string? ChannelId { get; init; }
    public string? ChannelUrl { get; init; }

    /// <summary>yyyy-MM-dd</summary>
    public string? UploadDate { get; init; }

    public double? DurationSeconds { get; init; }
    public string? Language { get; init; }
    public string? Description { get; init; }
    public PlaylistInfo? Playlist { get; init; }

    /// <summary>File name of the video (without folder).</summary>
    public string? File { get; init; }

    public IReadOnlyList<SubtitleInfo> Subtitles { get; init; } = [];
    public DateTimeOffset DownloadedAt { get; init; }
}

public sealed record PlaylistInfo(string? Id, string? Title, int? Index);

/// <param name="Language">Language code as used by YouTube, e.g. "ru".</param>
/// <param name="Automatic">True for YouTube's automatic captions (speech recognition).</param>
/// <param name="File">Whisper JSON file.</param>
/// <param name="Srt">SRT file.</param>
public sealed record SubtitleInfo(string Language, bool Automatic, string File, string Srt);

public static class VideoInfoFile
{
    /// <summary>Suffix of the metadata file. Not ".json" alone, so it never collides with a transcript.</summary>
    public const string Suffix = ".info.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetPath(string folder, string id) => Path.Combine(folder, id + Suffix);

    public static string Serialize(VideoInfo info) => JsonSerializer.Serialize(info, JsonOptions);

    public static VideoInfo Deserialize(string json) =>
        JsonSerializer.Deserialize<VideoInfo>(json, JsonOptions) ?? throw new JsonException("Leere Datei.");

    /// <summary>Builds the compact metadata from yt-dlp's full info JSON (may be null if it is missing).</summary>
    public static VideoInfo Create(
        VideoEntry entry, JsonElement? ytDlpInfo, string? mediaFile, IReadOnlyList<SubtitleInfo> subtitles, DateTimeOffset now)
    {
        string? S(string name) => ytDlpInfo is { } info ? YtDlpClient.GetString(info, name) : null;

        double? duration = ytDlpInfo is { } i && i.TryGetProperty("duration", out var d) && d.ValueKind == JsonValueKind.Number
            ? d.GetDouble()
            : entry.Duration?.TotalSeconds;

        return new VideoInfo
        {
            Id = entry.Id,
            Title = S("title") ?? entry.Title,
            Url = S("webpage_url") ?? entry.Url,
            Channel = S("channel") ?? S("uploader") ?? entry.Channel,
            ChannelId = S("channel_id"),
            ChannelUrl = S("channel_url"),
            UploadDate = FormatDate(S("upload_date")),
            DurationSeconds = duration,
            Language = S("language"),
            Description = S("description"),
            Playlist = entry.PlaylistId is null && entry.PlaylistTitle is null
                ? null
                : new PlaylistInfo(entry.PlaylistId, entry.PlaylistTitle, entry.PlaylistIndex),
            File = mediaFile is null ? null : Path.GetFileName(mediaFile),
            Subtitles = subtitles,
            DownloadedAt = now,
        };
    }

    /// <summary>True if yt-dlp lists <paramref name="language"/> among the uploaded (not automatic) subtitles.</summary>
    public static bool IsManualSubtitle(JsonElement? ytDlpInfo, string language) =>
        ytDlpInfo is { } info &&
        info.TryGetProperty("subtitles", out var subs) &&
        subs.ValueKind == JsonValueKind.Object &&
        subs.TryGetProperty(language, out _);

    /// <summary>"20240501" -> "2024-05-01".</summary>
    internal static string? FormatDate(string? yyyymmdd) =>
        yyyymmdd is { Length: 8 } &&
        DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : yyyymmdd;
}
