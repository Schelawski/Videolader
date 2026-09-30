using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Videolader.Core;

/// <summary>One subtitle line with start and end in seconds.</summary>
public sealed record TranscriptSegment(double Start, double End, string Text);

/// <summary>
/// Reads YouTube subtitles (json3 or WebVTT) into clean, non-overlapping segments. Automatic captions
/// are shown by YouTube as "rolling" two-line captions; the parsers remove the repeated lines.
/// </summary>
public static partial class SubtitleParser
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"^(?:(\d+):)?(\d{1,2}):(\d{2})[.,](\d{1,3})")]
    private static partial Regex VttTime();

    public static IReadOnlyList<TranscriptSegment> Parse(string content, string extension) =>
        extension.TrimStart('.').ToLowerInvariant() switch
        {
            "json3" => ParseJson3(content),
            "vtt" => ParseVtt(content),
            _ => throw new NotSupportedException($"Untertitelformat {extension} wird nicht unterstützt."),
        };

    /// <summary>YouTube's own format: <c>{"events":[{"tStartMs":…,"dDurationMs":…,"segs":[{"utf8":"…"}]}]}</c>.</summary>
    public static IReadOnlyList<TranscriptSegment> ParseJson3(string json)
    {
        var raw = new List<(double Start, double End, string Text)>();
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("events", out var events) || events.ValueKind != JsonValueKind.Array)
            return [];

        foreach (var e in events.EnumerateArray())
        {
            if (!e.TryGetProperty("segs", out var segs) || segs.ValueKind != JsonValueKind.Array)
                continue;

            var text = new StringBuilder();
            foreach (var seg in segs.EnumerateArray())
            {
                if (seg.TryGetProperty("utf8", out var utf8) && utf8.ValueKind == JsonValueKind.String)
                    text.Append(utf8.GetString());
            }

            var clean = CleanText(text.ToString());
            if (clean.Length == 0)
                continue;

            var start = GetMs(e, "tStartMs");
            var duration = GetMs(e, "dDurationMs");
            raw.Add((start / 1000.0, (start + duration) / 1000.0, clean));
        }

        return Normalize(raw);
    }

    /// <summary>WebVTT, including YouTube's rolling automatic captions.</summary>
    public static IReadOnlyList<TranscriptSegment> ParseVtt(string vtt)
    {
        var raw = new List<(double Start, double End, string Text)>();
        var blocks = vtt.Replace("\r\n", "\n").Replace('\r', '\n').Split("\n\n");
        var recent = new Queue<string>();

        foreach (var block in blocks)
        {
            var lines = block.Split('\n');
            var timeIndex = Array.FindIndex(lines, l => l.Contains("-->", StringComparison.Ordinal));
            if (timeIndex < 0)
                continue;

            var times = lines[timeIndex].Split("-->", 2);
            var start = ParseVttTime(times[0]);
            var end = ParseVttTime(times[1]);
            if (start is null || end is null)
                continue;

            var newLines = new List<string>();
            foreach (var line in lines.Skip(timeIndex + 1))
            {
                var clean = CleanText(WebUtility.HtmlDecode(Tags().Replace(line, string.Empty)));
                if (clean.Length == 0 || recent.Contains(clean))
                    continue;

                newLines.Add(clean);
                recent.Enqueue(clean);
                if (recent.Count > 3)
                    recent.Dequeue();
            }

            if (newLines.Count > 0)
                raw.Add((start.Value, end.Value, string.Join(' ', newLines)));
        }

        return Normalize(raw);
    }

    /// <summary>Sorts, cuts overlaps (a line ends when the next one starts) and merges repeated text.</summary>
    internal static IReadOnlyList<TranscriptSegment> Normalize(List<(double Start, double End, string Text)> raw)
    {
        var sorted = raw.OrderBy(r => r.Start).ToList();
        var result = new List<TranscriptSegment>();

        for (var i = 0; i < sorted.Count; i++)
        {
            var (start, end, text) = sorted[i];
            if (i + 1 < sorted.Count && sorted[i + 1].Start < end && sorted[i + 1].Start > start)
                end = sorted[i + 1].Start;
            if (end < start)
                end = start;

            if (result.Count > 0 && result[^1].Text == text && start <= result[^1].End + 0.05)
            {
                result[^1] = result[^1] with { End = Math.Max(result[^1].End, end) };
                continue;
            }

            result.Add(new TranscriptSegment(Round(start), Round(end), text));
        }

        return result;
    }

    private static string CleanText(string text) => Whitespace().Replace(text, " ").Trim();

    private static double Round(double seconds) => Math.Round(seconds, 3, MidpointRounding.AwayFromZero);

    private static long GetMs(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt64() : 0;

    private static double? ParseVttTime(string text)
    {
        var match = VttTime().Match(text.Trim());
        if (!match.Success)
            return null;

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        var seconds = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
        var fraction = match.Groups[4].Value.PadRight(3, '0');
        var ms = int.Parse(fraction, CultureInfo.InvariantCulture);
        return hours * 3600 + minutes * 60 + seconds + ms / 1000.0;
    }
}

/// <summary>Writes segments as Whisper JSON and SRT.</summary>
public static class TranscriptWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// The layout of Whisper's JSON output:
    /// <c>{"text": "…", "segments": [{"id": 0, "start": 0.0, "end": 2.5, "text": "…"}], "language": "ru"}</c>.
    /// </summary>
    public static string ToWhisperJson(IReadOnlyList<TranscriptSegment> segments, string language)
    {
        var document = new WhisperDocument(
            string.Join(' ', segments.Select(s => s.Text)),
            segments.Select((s, i) => new WhisperSegment(i, s.Start, s.End, s.Text)).ToList(),
            language);
        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static string ToSrt(IReadOnlyList<TranscriptSegment> segments)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < segments.Count; i++)
        {
            builder.Append(i + 1).Append('\n');
            builder.Append(SrtTime(segments[i].Start)).Append(" --> ").Append(SrtTime(segments[i].End)).Append('\n');
            builder.Append(segments[i].Text).Append("\n\n");
        }

        return builder.ToString();
    }

    private static string SrtTime(double seconds)
    {
        var time = TimeSpan.FromMilliseconds(Math.Round(seconds * 1000));
        return string.Create(CultureInfo.InvariantCulture,
            $"{(int)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}");
    }

    private sealed record WhisperDocument(
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text,
        [property: System.Text.Json.Serialization.JsonPropertyName("segments")] IReadOnlyList<WhisperSegment> Segments,
        [property: System.Text.Json.Serialization.JsonPropertyName("language")] string Language);

    private sealed record WhisperSegment(
        [property: System.Text.Json.Serialization.JsonPropertyName("id")] int Id,
        [property: System.Text.Json.Serialization.JsonPropertyName("start")] double Start,
        [property: System.Text.Json.Serialization.JsonPropertyName("end")] double End,
        [property: System.Text.Json.Serialization.JsonPropertyName("text")] string Text);
}
