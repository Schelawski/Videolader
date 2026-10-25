namespace Videolader.Core;

/// <summary>What was typed on the command line.</summary>
public sealed class CliOptions
{
    public bool Help { get; set; }

    /// <summary><c>--version</c>: print the version and exit.</summary>
    public bool ShowVersion { get; set; }

    /// <summary>"setup": download or update yt-dlp, ffmpeg and Deno.</summary>
    public bool Setup { get; set; }

    /// <summary>Only list the videos and their status, download nothing.</summary>
    public bool ListOnly { get; set; }

    /// <summary>One JSON line per video on stdout; all other messages go to stderr.</summary>
    public bool Json { get; set; }

    /// <summary>Also show yt-dlp's own output lines.</summary>
    public bool Verbose { get; set; }

    /// <summary>Download videos even if they already exist.</summary>
    public bool NoSkip { get; set; }

    public List<string> Inputs { get; } = [];

    public string? OutputFolder { get; set; }

    public VideoQuality? Quality { get; set; }

    public string? SubtitleLanguages { get; set; }

    public bool NoSubtitles { get; set; }

    public int? Limit { get; set; }

    public int? PauseSeconds { get; set; }

    public string? CookiesBrowser { get; set; }

    /// <summary>Language of the messages for this run (<c>--lang</c>); null = from the settings.</summary>
    public UiLanguage? Language { get; set; }
}

public sealed record CliParseResult(CliOptions? Options, string? Error);

/// <summary>Reads the command line. Anything that is not an option is a link or ID.</summary>
public static class CliArguments
{
    /// <summary>Text of <c>--help</c> in the current language.</summary>
    public static string HelpText => UiText.CliHelp;

    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        var options = new CliOptions();
        var index = 0;

        if (args.Count > 0)
        {
            var first = args[0].ToLowerInvariant();
            if (first is "setup")
            {
                options.Setup = true;
                index = 1;
            }
            else if (first is "download")
            {
                index = 1;
            }
        }

        for (; index < args.Count; index++)
        {
            var arg = args[index];
            string? inlineValue = null;

            if (arg.StartsWith("--", StringComparison.Ordinal) && arg.Contains('='))
            {
                var split = arg.IndexOf('=');
                inlineValue = arg[(split + 1)..];
                arg = arg[..split];
            }

            string? Value() => inlineValue ?? (index + 1 < args.Count ? args[++index] : null);

            switch (arg.ToLowerInvariant())
            {
                case "--help" or "-h" or "-?" or "/?" or "help":
                    options.Help = true;
                    break;
                case "--version":
                    options.ShowVersion = true;
                    break;
                case "--list":
                    options.ListOnly = true;
                    break;
                case "--json":
                    options.Json = true;
                    break;
                case "--verbose" or "-v":
                    options.Verbose = true;
                    break;
                case "--no-skip":
                    options.NoSkip = true;
                    break;
                case "--no-subs":
                    options.NoSubtitles = true;
                    break;
                case "--out" or "-o":
                    if (Value() is not { Length: > 0 } folder)
                        return Fail(UiText.CliOutNeedsFolder);
                    options.OutputFolder = folder;
                    break;
                case "--quality" or "-q":
                    if (!TryParseQuality(Value(), out var quality))
                        return Fail(UiText.CliQualityValues);
                    options.Quality = quality;
                    break;
                case "--subs":
                    if (Value() is not { Length: > 0 } subs)
                        return Fail(UiText.CliSubsNeedLanguages);
                    options.SubtitleLanguages = subs;
                    break;
                case "--limit" or "-n":
                    if (!int.TryParse(Value(), out var limit) || limit < 0)
                        return Fail(UiText.CliLimitNeedsNumber);
                    options.Limit = limit;
                    break;
                case "--pause":
                    if (!int.TryParse(Value(), out var pause) || pause < 0)
                        return Fail(UiText.CliPauseNeedsSeconds);
                    options.PauseSeconds = pause;
                    break;
                case "--cookies":
                    if (Value() is not { Length: > 0 } browser)
                        return Fail(UiText.CliCookiesNeedBrowser);
                    options.CookiesBrowser = browser;
                    break;
                case "--lang":
                    if (UiLanguages.FromCode(Value()) is not { } language)
                        return Fail(UiText.CliLangValues);
                    options.Language = language;
                    break;
                case "--id" or "--playlist" or "--url":
                    if (Value() is not { Length: > 0 } input)
                        return Fail(UiText.CliOptionNeedsValue(arg));
                    options.Inputs.Add(input);
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                        return Fail(UiText.CliUnknownOption(arg));
                    options.Inputs.Add(arg);
                    break;
            }
        }

        if (!options.Help && !options.ShowVersion && !options.Setup && options.Inputs.Count == 0)
            return Fail(UiText.CliNeedLinkOrId);

        return new CliParseResult(options, null);
    }

    private static CliParseResult Fail(string message) => new(null, message);

    private static bool TryParseQuality(string? text, out VideoQuality quality)
    {
        quality = VideoQuality.Max1080;
        switch (text?.Trim().ToLowerInvariant())
        {
            case "best":
                quality = VideoQuality.Best;
                return true;
            case "1080" or "1080p":
                quality = VideoQuality.Max1080;
                return true;
            case "720" or "720p":
                quality = VideoQuality.Max720;
                return true;
            case "480" or "480p":
                quality = VideoQuality.Max480;
                return true;
            case "audio":
                quality = VideoQuality.AudioOnly;
                return true;
            default:
                return false;
        }
    }
}
