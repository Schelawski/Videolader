namespace Videolader.Core;

/// <summary>What was typed on the command line.</summary>
public sealed class CliOptions
{
    public bool Help { get; set; }

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
}

public sealed record CliParseResult(CliOptions? Options, string? Error);

/// <summary>Reads the command line. Anything that is not an option is a link or ID.</summary>
public static class CliArguments
{
    public const string HelpText = """
        Videolader – YouTube-Videos, Playlists und Kanäle herunterladen

        Aufruf:
          Videolader.exe <Link-oder-ID> [<Link-oder-ID> ...] [Optionen]
          Videolader.exe setup            yt-dlp, ffmpeg und Deno herunterladen bzw. aktualisieren
          Videolader.exe --help

        Ohne Argumente startet die Oberfläche.

        Eingaben (beliebig viele, auch gemischt):
          Video-Link oder Video-ID, Playlist-Link oder -ID (PL...), Kanal-Link (https://www.youtube.com/@Name)
          oder @Name. Mit --id, --playlist oder --url lässt sich dasselbe schreiben.

        Optionen (nicht angegeben = Wert aus den gespeicherten Einstellungen):
          --out <Ordner>       Zielordner
          --quality <Wert>     best, 1080, 720, 480 oder audio
          --subs <Sprachen>    Untertitel-Sprachen, z. B. ru,de
          --no-subs            keine Untertitel
          --limit <N>          höchstens N Videos pro Lauf (0 = alle)
          --pause <Sekunden>   Pause zwischen zwei Downloads
          --cookies <Browser>  firefox, chrome, edge oder brave
          --no-skip            auch bereits vorhandene Videos erneut laden
          --list               nur auflisten, nichts herunterladen
          --json               pro Video eine JSON-Zeile auf stdout, Meldungen auf stderr
          --verbose            auch die Ausgabe von yt-dlp zeigen

        Exitcodes: 0 = alles in Ordnung, 1 = mindestens ein Fehler, 2 = falsche Eingabe,
                   3 = Werkzeuge fehlen (dann "Videolader.exe setup"), 130 = abgebrochen.

        Hinweis: Videolader.exe ist eine Windows-Anwendung. In der Konsole kehrt die Eingabeaufforderung
        sofort zurück, die Ausgabe erscheint trotzdem. Zum Warten: "start /wait Videolader.exe ..." (cmd)
        oder "Videolader.exe ... | Out-Host" (PowerShell).
        """;

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
                        return Fail("--out braucht einen Ordner.");
                    options.OutputFolder = folder;
                    break;
                case "--quality" or "-q":
                    if (!TryParseQuality(Value(), out var quality))
                        return Fail("--quality: erlaubt sind best, 1080, 720, 480 und audio.");
                    options.Quality = quality;
                    break;
                case "--subs":
                    if (Value() is not { Length: > 0 } subs)
                        return Fail("--subs braucht Sprachen, z. B. ru,de.");
                    options.SubtitleLanguages = subs;
                    break;
                case "--limit" or "-n":
                    if (!int.TryParse(Value(), out var limit) || limit < 0)
                        return Fail("--limit braucht eine Zahl ab 0.");
                    options.Limit = limit;
                    break;
                case "--pause":
                    if (!int.TryParse(Value(), out var pause) || pause < 0)
                        return Fail("--pause braucht eine Anzahl Sekunden ab 0.");
                    options.PauseSeconds = pause;
                    break;
                case "--cookies":
                    if (Value() is not { Length: > 0 } browser)
                        return Fail("--cookies braucht einen Browser (firefox, chrome, edge, brave).");
                    options.CookiesBrowser = browser;
                    break;
                case "--id" or "--playlist" or "--url":
                    if (Value() is not { Length: > 0 } input)
                        return Fail($"{arg} braucht einen Wert.");
                    options.Inputs.Add(input);
                    break;
                default:
                    if (arg.StartsWith("--", StringComparison.Ordinal))
                        return Fail($"Unbekannte Option: {arg}");
                    options.Inputs.Add(arg);
                    break;
            }
        }

        if (!options.Help && !options.Setup && options.Inputs.Count == 0)
            return Fail("Bitte mindestens einen Link oder eine ID angeben. Hilfe: Videolader.exe --help");

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
