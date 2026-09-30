using System.Text.Json;
using Videolader.Core;

namespace Videolader.Tests;

public class VideoLibraryTests
{
    [Fact]
    public void Finds_media_file_named_after_the_id()
    {
        using var folder = new TempFolder();
        folder.File("dQw4w9WgXcQ.info.json", "{}");
        folder.File("dQw4w9WgXcQ.ru.srt");
        var library = VideoLibrary.Open(folder.Path);
        Assert.False(library.IsDownloaded("dQw4w9WgXcQ"));

        folder.File("dQw4w9WgXcQ.mp4");

        Assert.True(library.IsDownloaded("dQw4w9WgXcQ"));
        Assert.EndsWith("dQw4w9WgXcQ.mp4", library.FindMediaFile("dQw4w9WgXcQ"));
    }

    [Fact]
    public void Archive_is_written_and_read_again()
    {
        using var folder = new TempFolder();
        var library = VideoLibrary.Open(folder.Path);
        library.MarkDownloaded("jNQXAC9IVRw");
        library.MarkDownloaded("jNQXAC9IVRw");

        var reopened = VideoLibrary.Open(folder.Path);

        Assert.True(reopened.IsDownloaded("jNQXAC9IVRw"));
        Assert.Single(File.ReadAllLines(reopened.ArchivePath));
        Assert.Equal("youtube jNQXAC9IVRw", File.ReadAllLines(reopened.ArchivePath)[0]);
    }

    [Theory]
    [InlineData("youtube dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("vimeo 123", null)]
    [InlineData("", null)]
    public void Parses_archive_lines(string line, string? expected) =>
        Assert.Equal(expected, VideoLibrary.ParseArchiveLine(line));

    [Fact]
    public void Missing_folder_means_nothing_downloaded() =>
        Assert.False(VideoLibrary.Open(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).IsDownloaded("dQw4w9WgXcQ"));
}

public class DownloadJobTests
{
    private const string Id = "dQw4w9WgXcQ";

    private static readonly DateTimeOffset Now = new(2026, 9, 30, 20, 0, 0, TimeSpan.FromHours(2));

    /// <summary>Pretends to be yt-dlp: writes the files it would write, based on the arguments.</summary>
    private static DownloadJob FakeJob(bool writeVideo = true, int exitCode = 0, params string[] lines) => new(
        (args, onLine, _) =>
        {
            var home = args[args.ToList().IndexOf("-P") + 1]["home:".Length..];
            var temp = args[args.ToList().LastIndexOf("-P") + 1]["temp:".Length..];

            foreach (var line in lines)
                onLine(line);

            File.WriteAllText(Path.Combine(temp, Id + ".info.json"), """
                {
                  "id": "dQw4w9WgXcQ", "title": "Лекция 12", "channel": "Ananda Vidya", "channel_id": "UC123",
                  "upload_date": "20240501", "duration": 3725, "webpage_url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                  "description": "Beschreibung", "language": "ru",
                  "subtitles": { "en": [] },
                  "automatic_captions": { "ru": [] }
                }
                """);
            File.WriteAllText(Path.Combine(temp, Id + ".ru.json3"), Samples.AutoCaptionsJson3);
            File.WriteAllText(Path.Combine(temp, Id + ".en.json3"), Samples.ManualJson3);
            if (writeVideo)
                File.WriteAllText(Path.Combine(home, Id + ".mp4"), "video");
            return Task.FromResult(exitCode);
        },
        () => Now);

    [Fact]
    public async Task Writes_video_info_and_subtitles_next_to_each_other()
    {
        using var folder = new TempFolder();
        var library = VideoLibrary.Open(folder.Path);
        var settings = new AppSettings { SubtitleLanguages = "ru,en" };
        var progress = new ListProgress<DownloadUpdate>();

        var result = await FakeJob(lines: ["VLPROG downloading 50 100 NA 10 5", "[Merger] Merging formats"])
            .RunAsync(Samples.Entry(Id), settings, library, progress, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(Path.Combine(folder.Path, Id + ".mp4")));
        Assert.True(File.Exists(Path.Combine(folder.Path, Id + ".ru.json")));
        Assert.True(File.Exists(Path.Combine(folder.Path, Id + ".ru.srt")));
        Assert.True(File.Exists(Path.Combine(folder.Path, Id + ".en.json")));
        Assert.True(library.IsInArchive(Id));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, DownloadJob.TempFolderName)));
        Assert.Contains(progress.Items, u => u.Percent == 50);
        Assert.Contains(progress.Items, u => u.Phase == "Zusammenführen…");

        var info = VideoInfoFile.Deserialize(File.ReadAllText(VideoInfoFile.GetPath(folder.Path, Id)));
        Assert.Equal(Id, info.Id);
        Assert.Equal("Лекция 12", info.Title);
        Assert.Equal("Ananda Vidya", info.Channel);
        Assert.Equal("2024-05-01", info.UploadDate);
        Assert.Equal(3725, info.DurationSeconds);
        Assert.Equal(Id + ".mp4", info.File);
        Assert.Equal(new PlaylistInfo("PLabcdefghijklmnop", "Satsang 2026", 3), info.Playlist);
        Assert.Equal(Now, info.DownloadedAt);

        var en = Assert.Single(info.Subtitles, s => s.Language == "en");
        Assert.False(en.Automatic);
        var ru = Assert.Single(info.Subtitles, s => s.Language == "ru");
        Assert.True(ru.Automatic);
        Assert.Equal(Id + ".ru.json", ru.File);

        using var whisper = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder.Path, Id + ".ru.json")));
        Assert.Equal("Добрый вечер", whisper.RootElement.GetProperty("segments")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Info_json_keeps_cyrillic_readable_and_uses_camel_case()
    {
        using var folder = new TempFolder();
        var library = VideoLibrary.Open(folder.Path);

        await FakeJob().RunAsync(Samples.Entry(Id), new AppSettings(), library, new ListProgress<DownloadUpdate>(), CancellationToken.None);

        var json = File.ReadAllText(VideoInfoFile.GetPath(folder.Path, Id));
        Assert.Contains("\"title\": \"Лекция 12\"", json);
        Assert.Contains("\"uploadDate\"", json);
    }

    [Fact]
    public async Task Without_subtitles_no_transcripts_are_written()
    {
        using var folder = new TempFolder();
        var library = VideoLibrary.Open(folder.Path);

        var result = await FakeJob().RunAsync(
            Samples.Entry(Id), new AppSettings { DownloadSubtitles = false }, library, new ListProgress<DownloadUpdate>(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(result.Subtitles);
        Assert.False(File.Exists(Path.Combine(folder.Path, Id + ".ru.json")));
    }

    [Fact]
    public async Task Missing_video_is_a_failure_with_the_yt_dlp_error()
    {
        using var folder = new TempFolder();
        var library = VideoLibrary.Open(folder.Path);

        var result = await FakeJob(false, 1, "ERROR: [youtube] dQw4w9WgXcQ: Private video. Sign in if you've been granted access")
            .RunAsync(Samples.Entry(Id), new AppSettings(), library, new ListProgress<DownloadUpdate>(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.StartsWith("Private video", result.Error);
        Assert.False(library.IsInArchive(Id));
        Assert.False(File.Exists(VideoInfoFile.GetPath(folder.Path, Id)));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void Round_trip_and_normalize()
    {
        using var primary = new TempFolder();
        using var fallback = new TempFolder();
        var store = new SettingsStore(primary.Path, fallback.Path);

        store.Save(new AppSettings { OutputFolder = @"D:\Видео", SubtitleLanguages = " ru ; de,ru ", Quality = VideoQuality.Max720, PauseSeconds = 9999 });
        var loaded = store.Load();

        Assert.Equal(@"D:\Видео", loaded.OutputFolder);
        Assert.Equal("ru,de", loaded.SubtitleLanguages);
        Assert.Equal(VideoQuality.Max720, loaded.Quality);
        Assert.Equal(AppSettings.MaxPauseSeconds, loaded.PauseSeconds);
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        using var primary = new TempFolder();
        using var fallback = new TempFolder();
        primary.File(SettingsStore.FileName, "{ not json");

        var loaded = new SettingsStore(primary.Path, fallback.Path).Load();

        Assert.Equal(VideoQuality.Max1080, loaded.Quality);
        Assert.True(loaded.SkipExisting);
    }
}
