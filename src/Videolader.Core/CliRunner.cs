using System.Text.Json;
using System.Text.Json.Serialization;

namespace Videolader.Core;

/// <summary>
/// Runs Videolader without a window: the same steps as the window (list, check, download), written as text.
/// Exit codes: 0 ok, 1 at least one error, 2 wrong input, 3 tools missing, 130 cancelled.
/// </summary>
public sealed class CliRunner(
    HttpClient http,
    AppSettings settings,
    string toolsDirectory,
    string appDirectory,
    TextWriter stdout,
    TextWriter stderr)
{
    public const int ExitOk = 0;
    public const int ExitFailed = 1;
    public const int ExitBadInput = 2;
    public const int ExitToolsMissing = 3;
    public const int ExitCancelled = 130;

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private TextWriter _log = TextWriter.Null;
    private bool _json;

    public async Task<int> RunAsync(CliOptions options, CancellationToken ct)
    {
        _json = options.Json;
        _log = options.Json ? stderr : stdout;

        try
        {
            return options.Setup
                ? await SetupAsync(ct).ConfigureAwait(false)
                : await DownloadAsync(options, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _log.WriteLine(UiText.Cancelled);
            return ExitCancelled;
        }
    }

    private async Task<int> DownloadAsync(CliOptions options, CancellationToken ct)
    {
        ApplyOptions(options);

        var parsed = InputParser.Parse(string.Join('\n', options.Inputs));
        foreach (var invalid in parsed.Invalid)
            _log.WriteLine(UiText.NotRecognized(invalid));
        if (parsed.Items.Count == 0)
        {
            stderr.WriteLine(UiText.CliNoValidLinks);
            return ExitBadInput;
        }

        var tools = ToolLocator.Locate(toolsDirectory, appDirectory);
        if (tools.YtDlp is null || (!options.ListOnly && tools.Ffmpeg is null))
        {
            stderr.WriteLine(UiText.CliToolsMissing);
            return ExitToolsMissing;
        }

        if (!options.ListOnly && tools.Deno is null)
            _log.WriteLine(UiText.CliDenoMissing);

        var folder = settings.OutputFolder;
        if (string.IsNullOrWhiteSpace(folder))
            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Videolader");
        folder = Path.GetFullPath(folder);

        var library = VideoLibrary.Open(folder);
        var log = new SyncProgress<string>(line => _log.WriteLine(line));
        var language = SubtitleLanguageList.Split(settings.SubtitleLanguages).FirstOrDefault();
        var resolver = new VideoResolver(http, new YtDlpClient(tools), settings.ApiKey, settings.CookiesBrowser, log, language);

        _log.WriteLine(UiText.CliLoadingList);
        var entries = await resolver.ResolveAsync(parsed.Items, ct).ConfigureAwait(false);
        _log.WriteLine(UiText.CliVideosFound(entries.Count, folder));

        var skipExisting = settings.SkipExisting && !options.NoSkip;
        var job = new DownloadJob(new YtDlpClient(tools));
        int done = 0, failed = 0, skipped = 0, unavailable = 0, remaining = 0;
        var needsPause = false;
        var limitReached = false;

        for (var i = 0; i < entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var entry = entries[i];

            if (entry.Unavailable)
            {
                unavailable++;
                Emit(entry, "unavailable");
                continue;
            }

            var exists = library.IsDownloaded(entry.Id);
            if (options.ListOnly)
            {
                Emit(entry, exists ? "exists" : "new");
                continue;
            }

            if (skipExisting && exists)
            {
                skipped++;
                Emit(entry, "skipped");
                continue;
            }

            if (settings.MaxVideosPerRun > 0 && done + failed >= settings.MaxVideosPerRun)
            {
                limitReached = true;
                remaining++;
                continue;
            }

            if (needsPause && settings.PauseSeconds > 0)
                await Task.Delay(PauseTiming.Next(settings.PauseSeconds, settings.RandomPause), ct).ConfigureAwait(false);
            needsPause = true;

            _log.WriteLine($"[{i + 1}/{entries.Count}] {entry.Id} – {entry.Title ?? UiText.TitleUnknown}");

            var progress = new SyncProgress<DownloadUpdate>(update =>
            {
                if (options.Verbose && update.LogLine is not null)
                    _log.WriteLine("    " + update.LogLine);
            });

            DownloadResult result;
            try
            {
                result = await job.RunAsync(entry, settings, library, progress, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
            {
                result = DownloadResult.Failed(ex.Message);
            }

            if (result.Success)
            {
                done++;
                _log.WriteLine("    " + UiText.CliDone(Path.GetFileName(result.MediaFile)!, [.. result.Subtitles.Select(s => s.Language)]));
                Emit(entry, "downloaded", file: result.MediaFile);
            }
            else
            {
                failed++;
                _log.WriteLine("    " + UiText.Error(result.Error ?? string.Empty));
                if (result.Error is not null && YtDlpOutput.GetHint(result.Error) is { } hint)
                    _log.WriteLine("    " + UiText.Tip(hint));
                Emit(entry, "failed", error: result.Error);

                // Every further video would fail the same way.
                if (result.Error is not null && YtDlpOutput.IsCookieError(result.Error))
                {
                    _log.WriteLine(UiText.CliCookieStop);
                    break;
                }
            }
        }

        if (options.ListOnly)
        {
            _log.WriteLine(UiText.CliListOnly);
            return ExitOk;
        }

        _log.WriteLine(UiText.CliFinished(done, failed, skipped, unavailable));
        if (limitReached)
            _log.WriteLine(UiText.CliLimitReached(settings.MaxVideosPerRun, remaining));

        return failed > 0 ? ExitFailed : ExitOk;
    }

    private async Task<int> SetupAsync(CancellationToken ct)
    {
        var tools = ToolLocator.Locate(toolsDirectory, appDirectory);
        var installer = new ToolInstaller(http);
        var lastPrinted = new Dictionary<string, int>();
        var progress = new SyncProgress<ToolDownloadProgress>(p =>
        {
            if (p.Percent is not { } percent)
                return;
            var step = (int)percent / 20 * 20;
            if (lastPrinted.GetValueOrDefault(p.Tool, -1) < step)
            {
                lastPrinted[p.Tool] = step;
                _log.WriteLine($"    {p.Tool}: {step} %");
            }
        });

        try
        {
            if (tools.YtDlp is null)
            {
                _log.WriteLine(UiText.DownloadingTool("yt-dlp"));
                await installer.InstallYtDlpAsync(toolsDirectory, progress, ct).ConfigureAwait(false);
            }

            if (tools.Ffmpeg is null)
            {
                _log.WriteLine(UiText.DownloadingTool("ffmpeg"));
                await installer.InstallFfmpegAsync(toolsDirectory, progress, ct).ConfigureAwait(false);
            }

            if (tools.Deno is null)
            {
                _log.WriteLine(UiText.DownloadingTool("Deno"));
                await installer.InstallDenoAsync(toolsDirectory, progress, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            stderr.WriteLine(UiText.CliSetupFailed(ex.Message));
            return ExitFailed;
        }

        tools = ToolLocator.Locate(toolsDirectory, appDirectory);
        if (tools.YtDlp is not null)
        {
            _log.WriteLine(UiText.CheckingForUpdates("yt-dlp"));
            await new YtDlpClient(tools).UpdateAsync(line => _log.WriteLine("    " + line), ct).ConfigureAwait(false);
        }

        _log.WriteLine(tools.IsComplete ? UiText.AllToolsReady : UiText.ToolsStillMissing);
        return tools.IsComplete ? ExitOk : ExitFailed;
    }

    /// <summary>Command line values replace the stored settings for this run only; nothing is saved.</summary>
    private void ApplyOptions(CliOptions options)
    {
        if (options.OutputFolder is not null)
            settings.OutputFolder = options.OutputFolder;
        if (options.Quality is { } quality)
            settings.Quality = quality;
        if (options.SubtitleLanguages is not null)
            settings.SubtitleLanguages = options.SubtitleLanguages;
        if (options.NoSubtitles)
            settings.DownloadSubtitles = false;
        else if (options.SubtitleLanguages is not null)
            settings.DownloadSubtitles = true;
        if (options.Limit is { } limit)
            settings.MaxVideosPerRun = limit;
        if (options.PauseSeconds is { } pause)
            settings.PauseSeconds = pause;
        if (options.CookiesBrowser is not null)
            settings.CookiesBrowser = options.CookiesBrowser;
        settings.Normalize();
    }

    private void Emit(VideoEntry entry, string status, string? file = null, string? error = null)
    {
        if (!_json)
        {
            if (status is "unavailable" or "skipped" or "exists" or "new")
                _log.WriteLine($"{UiText.CliStatus(status),-14} {entry.Id} – {entry.Title}");
            return;
        }

        stdout.WriteLine(JsonSerializer.Serialize(
            new { id = entry.Id, status, title = entry.Title, file, error }, JsonLineOptions));
    }

    /// <summary><see cref="Progress{T}"/> posts to the thread pool; in a console program messages must stay in order.</summary>
    private sealed class SyncProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
