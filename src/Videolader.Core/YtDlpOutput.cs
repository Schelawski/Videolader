using System.Globalization;

namespace Videolader.Core;

/// <summary>A progress line of yt-dlp (one per update, see <see cref="YtDlpCommandLine.ProgressPrefix"/>).</summary>
public sealed record DownloadProgress(string Status, long? Downloaded, long? Total, double? Speed, double? Eta)
{
    public double? Percent => Downloaded is not null && Total is > 0
        ? Math.Clamp(100.0 * Downloaded.Value / Total.Value, 0, 100)
        : null;
}

/// <summary>Interprets the console output of yt-dlp.</summary>
public static class YtDlpOutput
{
    /// <summary>Parses "VLPROG downloading 1024 4096 NA 512.5 6". Missing values are "NA" or "None".</summary>
    public static bool TryParseProgress(string line, out DownloadProgress progress)
    {
        progress = null!;
        if (!line.StartsWith(YtDlpCommandLine.ProgressPrefix + " ", StringComparison.Ordinal))
            return false;

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 7)
            return false;

        var downloaded = ParseLong(parts[2]);
        var total = ParseLong(parts[3]) ?? ParseLong(parts[4]);
        progress = new DownloadProgress(parts[1], downloaded, total, ParseDouble(parts[5]), ParseDouble(parts[6]));
        return true;
    }

    /// <summary>Returns the message of an "ERROR: …" line, otherwise null.</summary>
    public static string? TryGetError(string line)
    {
        if (!line.StartsWith("ERROR:", StringComparison.Ordinal))
            return null;

        var message = line["ERROR:".Length..].Trim();
        // "[youtube] dQw4w9WgXcQ: Video unavailable" -> "Video unavailable"
        if (message.StartsWith('['))
        {
            var colon = message.IndexOf(": ", StringComparison.Ordinal);
            if (colon > 0)
                message = message[(colon + 2)..];
        }

        return message;
    }

    /// <summary>Recognizes the step yt-dlp is working on, for the status column.</summary>
    public static string? DetectPhase(string line)
    {
        if (line.StartsWith("[Merger]", StringComparison.Ordinal))
            return "Zusammenführen…";
        if (line.StartsWith("[ExtractAudio]", StringComparison.Ordinal))
            return "Audio umwandeln…";
        if (line.StartsWith("[info] Writing video subtitles", StringComparison.Ordinal) ||
            line.StartsWith("[info] Downloading subtitles", StringComparison.Ordinal))
            return "Untertitel…";
        if (line.Contains("has already been downloaded", StringComparison.Ordinal))
            return "Bereits vorhanden";
        return null;
    }

    /// <summary>Lines that start a new download stream (video, audio, subtitle).</summary>
    public static bool IsNewStream(string line) =>
        line.StartsWith("[download] Destination:", StringComparison.Ordinal);

    /// <summary>Well-known yt-dlp errors translated into a hint for the user.</summary>
    public static string? GetHint(string error)
    {
        if (error.Contains("Sign in to confirm", StringComparison.OrdinalIgnoreCase))
            return "YouTube verlangt eine Anmeldung. Wähle unter „Cookies“ einen Browser, in dem du bei YouTube angemeldet bist (Firefox funktioniert am zuverlässigsten).";
        if (error.Contains("HTTP Error 429", StringComparison.OrdinalIgnoreCase))
            return "YouTube drosselt die Anfragen (429). Warte etwas und erhöhe die Pause zwischen den Downloads.";
        if (error.Contains("Private video", StringComparison.OrdinalIgnoreCase))
            return "Das Video ist privat.";
        if (error.Contains("JavaScript runtime", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("n challenge", StringComparison.OrdinalIgnoreCase))
            return "Deno fehlt oder ist veraltet. Klicke auf „Werkzeuge einrichten“.";
        if (error.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffmpeg fehlt. Klicke auf „Werkzeuge einrichten“.";
        return null;
    }

    private static long? ParseLong(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? (long)value : null;

    private static double? ParseDouble(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
