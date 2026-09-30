using System.Text.Json;

namespace Videolader.Core;

/// <summary>Something happened while downloading: a log line, a new percentage or a new step.</summary>
public sealed record DownloadUpdate(string? LogLine = null, double? Percent = null, string? Phase = null);

public sealed record DownloadResult(bool Success, string? Error, string? MediaFile, IReadOnlyList<SubtitleInfo> Subtitles)
{
    public static DownloadResult Failed(string error) => new(false, error, null, []);
}

/// <summary>
/// Downloads one video with yt-dlp and writes the files next to it:
/// <list type="bullet">
/// <item><c>&lt;ID&gt;.mp4</c> – the video (or <c>.m4a</c> for audio only)</item>
/// <item><c>&lt;ID&gt;.info.json</c> – ID, title, channel, date, duration, description, playlist</item>
/// <item><c>&lt;ID&gt;.&lt;lang&gt;.json</c> – subtitles in Whisper's JSON layout</item>
/// <item><c>&lt;ID&gt;.&lt;lang&gt;.srt</c> – the same subtitles as SRT</item>
/// </list>
/// </summary>
public sealed class DownloadJob
{
    /// <summary>Folder inside the output folder for partial and raw files.</summary>
    public const string TempFolderName = ".videolader-temp";

    /// <summary>Runs yt-dlp with the given arguments and reports every output line.</summary>
    public delegate Task<int> YtDlpRunner(IReadOnlyList<string> arguments, Action<string> onLine, CancellationToken ct);

    private readonly YtDlpRunner _runYtDlp;
    private readonly Func<DateTimeOffset> _clock;

    public DownloadJob(YtDlpClient ytDlp)
        : this(ytDlp.RunAsync, () => DateTimeOffset.Now)
    {
    }

    public DownloadJob(YtDlpRunner runYtDlp, Func<DateTimeOffset> clock)
    {
        _runYtDlp = runYtDlp;
        _clock = clock;
    }

    public async Task<DownloadResult> RunAsync(
        VideoEntry entry,
        AppSettings settings,
        VideoLibrary library,
        IProgress<DownloadUpdate> progress,
        CancellationToken ct)
    {
        var outputFolder = library.Folder;
        var tempFolder = Path.Combine(outputFolder, TempFolderName);
        Directory.CreateDirectory(outputFolder);
        CreateHiddenFolder(tempFolder);

        var options = new DownloadOptions(
            entry.Id,
            outputFolder,
            tempFolder,
            settings.Quality,
            settings.DownloadSubtitles,
            SubtitleLanguageList.Split(settings.SubtitleLanguages),
            settings.CookiesBrowser);

        string? lastError = null;
        var mediaStream = 0;
        var inSubtitleStream = false;
        void OnLine(string line)
        {
            if (YtDlpOutput.TryParseProgress(line, out var p))
            {
                // Subtitles are tiny; only video/audio streams move the progress bar.
                if (p.Percent is { } percent && !inSubtitleStream)
                    progress.Report(new DownloadUpdate(Percent: percent, Phase: mediaStream > 1 ? $"Lädt Teil {mediaStream}" : "Lädt"));
                return;
            }

            if (YtDlpOutput.IsNewStream(line))
            {
                inSubtitleStream = line.EndsWith(".json3", StringComparison.OrdinalIgnoreCase) ||
                                   line.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase);
                if (!inSubtitleStream)
                    mediaStream++;
            }

            lastError = YtDlpOutput.TryGetError(line) ?? lastError;
            progress.Report(new DownloadUpdate(LogLine: line, Phase: YtDlpOutput.DetectPhase(line)));
        }

        var exitCode = await _runYtDlp(YtDlpCommandLine.BuildDownload(options), OnLine, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        var mediaFile = library.FindMediaFile(entry.Id);
        if (mediaFile is null)
        {
            DeleteTempFiles(tempFolder, entry.Id, keepPartial: true);
            var error = lastError ?? $"yt-dlp beendet mit Code {exitCode}, keine Videodatei gefunden.";
            return DownloadResult.Failed(error);
        }

        progress.Report(new DownloadUpdate(Phase: "Dateien schreiben…"));
        using var infoDocument = ReadYtDlpInfo(tempFolder, entry.Id);
        JsonElement? info = infoDocument?.RootElement;

        var subtitles = settings.DownloadSubtitles
            ? ConvertSubtitles(tempFolder, outputFolder, entry.Id, info, progress)
            : [];

        var videoInfo = VideoInfoFile.Create(entry, info, mediaFile, subtitles, _clock());
        await File.WriteAllTextAsync(VideoInfoFile.GetPath(outputFolder, entry.Id), VideoInfoFile.Serialize(videoInfo), CancellationToken.None)
            .ConfigureAwait(false);

        // Update the list entry with what yt-dlp knows (e.g. title when the list had none).
        entry.Title ??= videoInfo.Title;
        library.MarkDownloaded(entry.Id);
        DeleteTempFiles(tempFolder, entry.Id, keepPartial: false);

        if (exitCode != 0 && lastError is not null)
            progress.Report(new DownloadUpdate(LogLine: $"Hinweis: {lastError}"));

        return new DownloadResult(true, null, mediaFile, subtitles);
    }

    private static JsonDocument? ReadYtDlpInfo(string tempFolder, string id)
    {
        var path = Directory.EnumerateFiles(tempFolder, id + "*.info.json").FirstOrDefault();
        if (path is null)
            return null;

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    /// <summary>Converts the raw subtitles in the temp folder into <c>&lt;ID&gt;.&lt;lang&gt;.json</c> and <c>.srt</c>.</summary>
    internal static List<SubtitleInfo> ConvertSubtitles(
        string tempFolder, string outputFolder, string id, JsonElement? info, IProgress<DownloadUpdate> progress)
    {
        var result = new List<SubtitleInfo>();
        var prefix = id + ".";

        var files = Directory.EnumerateFiles(tempFolder, id + ".*")
            .Where(f => Path.GetExtension(f) is ".json3" or ".vtt")
            .Order(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file); // "<ID>.<lang>"
            if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
                continue;

            var language = name[prefix.Length..];
            if (result.Any(r => r.Language == language))
                continue;

            try
            {
                var segments = SubtitleParser.Parse(File.ReadAllText(file), Path.GetExtension(file));
                if (segments.Count == 0)
                {
                    progress.Report(new DownloadUpdate(LogLine: $"Untertitel {language}: leer, übersprungen."));
                    continue;
                }

                var jsonName = $"{id}.{language}.json";
                var srtName = $"{id}.{language}.srt";
                File.WriteAllText(Path.Combine(outputFolder, jsonName), TranscriptWriter.ToWhisperJson(segments, language));
                File.WriteAllText(Path.Combine(outputFolder, srtName), TranscriptWriter.ToSrt(segments));

                var automatic = !VideoInfoFile.IsManualSubtitle(info, language);
                result.Add(new SubtitleInfo(language, automatic, jsonName, srtName));
                progress.Report(new DownloadUpdate(LogLine:
                    $"Untertitel {language} ({(automatic ? "automatisch" : "hochgeladen")}): {segments.Count} Abschnitte → {jsonName}"));
            }
            catch (Exception ex) when (ex is JsonException or IOException or NotSupportedException)
            {
                progress.Report(new DownloadUpdate(LogLine: $"Untertitel {language} konnten nicht umgewandelt werden: {ex.Message}"));
            }
        }

        if (result.Count == 0)
            progress.Report(new DownloadUpdate(LogLine: "Keine Untertitel in den gewählten Sprachen gefunden."));

        return result;
    }

    private static void DeleteTempFiles(string tempFolder, string id, bool keepPartial)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(tempFolder, id + "*"))
            {
                // Partial downloads stay so a retry can continue them.
                if (keepPartial && (file.EndsWith(".part", StringComparison.Ordinal) || file.Contains(".part-Frag", StringComparison.Ordinal)))
                    continue;
                File.Delete(file);
            }

            if (!Directory.EnumerateFileSystemEntries(tempFolder).Any())
                Directory.Delete(tempFolder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Temp files are harmless; they are cleaned up next time.
        }
    }

    private static void CreateHiddenFolder(string path)
    {
        var info = Directory.CreateDirectory(path);
        if (OperatingSystem.IsWindows())
            info.Attributes |= FileAttributes.Hidden;
    }
}
