using Videolader.Core;

namespace Videolader.Tests;

public class YtDlpTests
{
    private static DownloadOptions Options(bool subtitles = true, string? cookies = null) => new(
        "-abcdefghij",
        Path.Combine("D:", "Videos"),
        Path.Combine("D:", "Videos", ".videolader-temp"),
        VideoQuality.Max1080,
        subtitles,
        ["ru", "de"],
        cookies);

    [Fact]
    public void Download_names_files_by_id_and_ends_with_separator_and_url()
    {
        var args = YtDlpCommandLine.BuildDownload(Options());

        Assert.Contains("%(id)s.%(ext)s", args);
        Assert.Contains("--no-playlist", args);
        Assert.Equal("--", args[^2]);
        Assert.Equal("https://www.youtube.com/watch?v=-abcdefghij", args[^1]);
        Assert.Equal("--ignore-config", args[0]);
    }

    [Fact]
    public void Download_with_subtitles_requests_json3_both_kinds()
    {
        var args = YtDlpCommandLine.BuildDownload(Options());

        Assert.Contains("--write-subs", args);
        Assert.Contains("--write-auto-subs", args);
        Assert.Equal("ru,de", args[args.ToList().IndexOf("--sub-langs") + 1]);
        Assert.Equal("json3/vtt", args[args.ToList().IndexOf("--sub-format") + 1]);
    }

    [Fact]
    public void Download_without_subtitles_and_with_cookies()
    {
        var args = YtDlpCommandLine.BuildDownload(Options(subtitles: false, cookies: "firefox"));

        Assert.DoesNotContain("--write-subs", args);
        Assert.Equal("firefox", args[args.ToList().IndexOf("--cookies-from-browser") + 1]);
    }

    [Theory]
    [InlineData(VideoQuality.Best, "res,vcodec:h264,acodec:m4a")]
    [InlineData(VideoQuality.Max720, "res:720,vcodec:h264,acodec:m4a")]
    public void Quality_selects_format_sorting(VideoQuality quality, string sort)
    {
        var args = quality.GetFormatArguments().ToList();

        Assert.Equal(sort, args[args.IndexOf("-S") + 1]);
        Assert.Equal("mp4", args[args.IndexOf("--merge-output-format") + 1]);
    }

    [Fact]
    public void Audio_only_extracts_m4a() =>
        Assert.Contains("-x", VideoQuality.AudioOnly.GetFormatArguments());

    [Fact]
    public void Parses_progress_lines()
    {
        Assert.True(YtDlpOutput.TryParseProgress("VLPROG downloading 1048576 4194304 NA 524288.5 6", out var p));
        Assert.Equal(25, p.Percent);
        Assert.Equal(524288.5, p.Speed);

        Assert.True(YtDlpOutput.TryParseProgress("VLPROG downloading 1000 NA 4000.0 NA NA", out var estimate));
        Assert.Equal(25, estimate.Percent);

        Assert.False(YtDlpOutput.TryParseProgress("[download] Destination: x.mp4", out _));
    }

    [Theory]
    [InlineData("ERROR: [youtube] abcdefghijk: Video unavailable", "Video unavailable")]
    [InlineData("ERROR: Unable to download", "Unable to download")]
    [InlineData("WARNING: something", null)]
    public void Extracts_errors(string line, string? expected) => Assert.Equal(expected, YtDlpOutput.TryGetError(line));

    [Fact]
    public void Parses_flat_playlist()
    {
        var json = """
            {
              "id": "PLabcdefghijklmnop", "title": "Satsang 2026", "_type": "playlist",
              "entries": [
                { "id": "dQw4w9WgXcQ", "title": "Lecture 1", "duration": 3725.0, "channel": "Ananda" },
                { "id": "jNQXAC9IVRw", "title": "[Private video]", "duration": null },
                { "id": "UCxxxxxxxxxxxxxxxxxxxxxx", "title": "Not a video" }
              ]
            }
            """;

        var list = YtDlpClient.ParseList(json);

        Assert.Equal("Satsang 2026", list.Title);
        Assert.Equal(2, list.Entries.Count);
        Assert.Equal(TimeSpan.FromSeconds(3725), list.Entries[0].Duration);
        Assert.Equal("Ananda", list.Entries[0].Channel);
        Assert.Equal(1, list.Entries[0].PlaylistIndex);
        Assert.False(list.Entries[0].Unavailable);
        Assert.True(list.Entries[1].Unavailable);
    }

    [Theory]
    [InlineData("PT1H2M3S", 3723)]
    [InlineData("PT45S", 45)]
    public void Parses_api_durations(string iso, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), YouTubeApiClient.ParseDuration(iso));

    [Fact]
    public void Live_stream_duration_is_unknown() => Assert.Null(YouTubeApiClient.ParseDuration("P0D"));

    [Fact]
    public void Reads_api_error_message() =>
        Assert.Equal("API key not valid.", YouTubeApiClient.ReadErrorMessage("""{"error":{"code":400,"message":"API key not valid."}}"""));
}
