using System.Text.Json;
using Videolader.Core;

namespace Videolader.Tests;

public class SubtitleTests
{
    [Fact]
    public void Json3_auto_captions_become_clean_segments_without_overlap()
    {
        var segments = SubtitleParser.ParseJson3(Samples.AutoCaptionsJson3);

        Assert.Equal(2, segments.Count);
        Assert.Equal(new TranscriptSegment(0.16, 2.96, "Добрый вечер"), segments[0]);
        Assert.Equal(new TranscriptSegment(2.96, 6.96, "сегодня мы поговорим"), segments[1]);
    }

    [Fact]
    public void Json3_manual_subtitles_join_line_breaks()
    {
        var segments = SubtitleParser.ParseJson3(Samples.ManualJson3);

        Assert.Equal(2, segments.Count);
        Assert.Equal(new TranscriptSegment(1.0, 3.5, "Hello and welcome"), segments[0]);
        Assert.Equal(new TranscriptSegment(4.0, 5.5, "to the lecture."), segments[1]);
    }

    [Fact]
    public void Vtt_rolling_captions_are_deduplicated()
    {
        var segments = SubtitleParser.ParseVtt(Samples.AutoCaptionsVtt);

        Assert.Equal(2, segments.Count);
        Assert.Equal("good evening", segments[0].Text);
        Assert.Equal(0.16, segments[0].Start);
        Assert.Equal("today we talk", segments[1].Text);
        Assert.Equal(2.96, segments[1].Start);
        Assert.Equal(6.96, segments[1].End);
    }

    [Fact]
    public void Vtt_without_hours_and_with_entities()
    {
        var vtt = "WEBVTT\n\n01:02.500 --> 01:04.000\nTom &amp; Jerry\n";

        var segment = Assert.Single(SubtitleParser.ParseVtt(vtt));

        Assert.Equal(new TranscriptSegment(62.5, 64.0, "Tom & Jerry"), segment);
    }

    [Fact]
    public void Whisper_json_has_text_segments_and_language()
    {
        TranscriptSegment[] segments = [new(0.16, 2.96, "Добрый вечер"), new(2.96, 6.96, "сегодня")];

        var json = TranscriptWriter.ToWhisperJson(segments, "ru");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("Добрый вечер сегодня", root.GetProperty("text").GetString());
        Assert.Equal("ru", root.GetProperty("language").GetString());
        var first = root.GetProperty("segments")[0];
        Assert.Equal(0, first.GetProperty("id").GetInt32());
        Assert.Equal(0.16, first.GetProperty("start").GetDouble());
        Assert.Equal(2.96, first.GetProperty("end").GetDouble());
        Assert.Equal("Добрый вечер", first.GetProperty("text").GetString());
        Assert.Contains("Добрый", json); // Cyrillic stays readable, not \u-escaped
    }

    [Fact]
    public void Srt_uses_comma_milliseconds_and_hours()
    {
        TranscriptSegment[] segments = [new(0.16, 2.96, "One"), new(3725.5, 3727, "Two")];

        var srt = TranscriptWriter.ToSrt(segments);

        Assert.Equal("1\n00:00:00,160 --> 00:00:02,960\nOne\n\n2\n01:02:05,500 --> 01:02:07,000\nTwo\n\n", srt);
    }
}
