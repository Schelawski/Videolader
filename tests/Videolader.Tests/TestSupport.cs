using Videolader.Core;

namespace Videolader.Tests;

/// <summary>Temporary folder that is deleted after the test.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "videolader-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name, string content = "")
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>Collects progress reports synchronously.</summary>
internal sealed class ListProgress<T> : IProgress<T>
{
    public List<T> Items { get; } = [];

    public void Report(T value)
    {
        lock (Items)
            Items.Add(value);
    }
}

internal static class Samples
{
    /// <summary>Automatic captions as YouTube delivers them in json3 (rolling, overlapping windows).</summary>
    public const string AutoCaptionsJson3 = """
        {
          "wireMagic": "pb3",
          "pens": [ {} ],
          "events": [
            { "tStartMs": 0, "dDurationMs": 7000, "id": 1, "wpWinPosId": 1, "wsWinStyleId": 1 },
            { "tStartMs": 160, "wWinId": 1, "dDurationMs": 4640,
              "segs": [ { "utf8": "Добрый", "acAsrConf": 0 }, { "utf8": " вечер", "tOffsetMs": 400, "acAsrConf": 0 } ] },
            { "tStartMs": 2950, "wWinId": 1, "dDurationMs": 1850, "aAppend": 1, "segs": [ { "utf8": "\n" } ] },
            { "tStartMs": 2960, "wWinId": 1, "dDurationMs": 4000,
              "segs": [ { "utf8": "сегодня", "acAsrConf": 0 }, { "utf8": " мы", "tOffsetMs": 300 }, { "utf8": " поговорим", "tOffsetMs": 700 } ] },
            { "tStartMs": 6960, "wWinId": 1, "dDurationMs": 10, "aAppend": 1, "segs": [ { "utf8": "\n" } ] }
          ]
        }
        """;

    /// <summary>Uploaded (manual) subtitles in json3: one event per cue, may contain line breaks.</summary>
    public const string ManualJson3 = """
        {
          "events": [
            { "tStartMs": 1000, "dDurationMs": 2500, "segs": [ { "utf8": "Hello and\nwelcome" } ] },
            { "tStartMs": 4000, "dDurationMs": 1500, "segs": [ { "utf8": "to the lecture." } ] }
          ]
        }
        """;

    /// <summary>Automatic captions as WebVTT: every line appears twice (rolling captions).</summary>
    public const string AutoCaptionsVtt =
        "WEBVTT\nKind: captions\nLanguage: en\n\n" +
        "00:00:00.160 --> 00:00:02.950 align:start position:0%\n \ngood<00:00:00.560><c> evening</c>\n\n" +
        "00:00:02.950 --> 00:00:02.960 align:start position:0%\ngood evening\n \n\n" +
        "00:00:02.960 --> 00:00:06.960 align:start position:0%\ngood evening\ntoday<00:00:03.260><c> we</c><00:00:03.660><c> talk</c>\n\n" +
        "00:00:06.960 --> 00:00:06.970 align:start position:0%\ntoday we talk\n \n";

    public static VideoEntry Entry(string id = "dQw4w9WgXcQ") => new(id)
    {
        Title = "Lecture 12",
        PlaylistId = "PLabcdefghijklmnop",
        PlaylistTitle = "Satsang 2026",
        PlaylistIndex = 3,
    };
}
