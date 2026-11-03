namespace Videolader.Core;

/// <summary>What to download for one video.</summary>
/// <param name="VideoId">11-character YouTube video ID.</param>
/// <param name="OutputFolder">Folder the video is saved to as <c>&lt;ID&gt;.mp4</c>.</param>
/// <param name="TempFolder">Folder for partial files, the raw info JSON and raw subtitles.</param>
public sealed record DownloadOptions(
    string VideoId,
    string OutputFolder,
    string TempFolder,
    VideoQuality Quality,
    bool Subtitles,
    IReadOnlyList<string> SubtitleLanguages,
    string? CookiesBrowser,
    string? ContentLanguage = null);

/// <summary>Builds yt-dlp command lines. Arguments are passed as a list, so no quoting is needed.</summary>
public static class YtDlpCommandLine
{
    /// <summary>Prefix of the machine-readable progress lines (see <see cref="YtDlpOutput"/>).</summary>
    public const string ProgressPrefix = "VLPROG";

    private const string ProgressTemplate =
        "download:" + ProgressPrefix +
        " %(progress.status)s %(progress.downloaded_bytes)s %(progress.total_bytes)s" +
        " %(progress.total_bytes_estimate)s %(progress.speed)s %(progress.eta)s";

    /// <summary>Options every call uses: ignore user config files, plain output.</summary>
    private static readonly string[] CommonArguments = ["--ignore-config", "--color", "never", "--encoding", "utf-8"];

    public static IReadOnlyList<string> BuildDownload(DownloadOptions options)
    {
        var args = new List<string>(CommonArguments)
        {
            "--no-playlist",
            "--newline",
            "--progress-template", ProgressTemplate,
            "--no-mtime",
            "--retries", "10",
            "--fragment-retries", "10",
            "-P", "home:" + options.OutputFolder,
            "-P", "temp:" + options.TempFolder,
            "-o", "%(id)s.%(ext)s",

            // Full metadata goes to the temp folder; Videolader writes its own compact <ID>.info.json.
            "--write-info-json",
            "--no-write-playlist-metafiles",
            "-o", "infojson:" + Path.Combine(options.TempFolder, "%(id)s.%(ext)s"),
        };

        args.AddRange(options.Quality.GetFormatArguments());

        if (options.Subtitles && options.SubtitleLanguages.Count > 0)
        {
            args.AddRange(
            [
                "--write-subs",
                "--write-auto-subs",
                "--sub-langs", string.Join(',', options.SubtitleLanguages),
                // json3 is YouTube's native format with clean timings; vtt as fallback.
                "--sub-format", "json3/vtt",
                "-o", "subtitle:" + Path.Combine(options.TempFolder, "%(id)s.%(ext)s"),
            ]);
        }

        AddCookies(args, options.CookiesBrowser);
        AddContentLanguage(args, options.ContentLanguage);

        // "--" so an ID starting with "-" is not read as an option.
        args.Add("--");
        args.Add(YouTubeUrls.Video(options.VideoId));
        return args;
    }

    /// <summary>Lists a playlist or channel tab without downloading anything (one JSON document on stdout).</summary>
    public static IReadOnlyList<string> BuildList(string url, string? cookiesBrowser, string? contentLanguage = null)
    {
        var args = new List<string>(CommonArguments) { "--flat-playlist", "-J", "--no-warnings" };
        AddCookies(args, cookiesBrowser);
        AddContentLanguage(args, contentLanguage);
        args.Add("--");
        args.Add(url);
        return args;
    }

    /// <summary>
    /// Without this YouTube answers in English and shows machine-translated titles instead of the original ones.
    /// </summary>
    private static void AddContentLanguage(List<string> args, string? language)
    {
        if (!string.IsNullOrWhiteSpace(language))
        {
            args.Add("--extractor-args");
            args.Add("youtube:lang=" + language.Trim());
        }
    }

    private static void AddCookies(List<string> args, string? cookiesBrowser)
    {
        if (!string.IsNullOrWhiteSpace(cookiesBrowser))
        {
            args.Add("--cookies-from-browser");
            args.Add(cookiesBrowser.Trim());
        }
    }
}
