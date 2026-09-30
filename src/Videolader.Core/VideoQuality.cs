namespace Videolader.Core;

/// <summary>Quality presets offered in the UI.</summary>
public enum VideoQuality
{
    /// <summary>Highest available resolution, saved as MP4.</summary>
    Best,

    /// <summary>At most 1080p, saved as MP4.</summary>
    Max1080,

    /// <summary>At most 720p, saved as MP4.</summary>
    Max720,

    /// <summary>At most 480p, saved as MP4 (small files).</summary>
    Max480,

    /// <summary>Audio only, saved as M4A.</summary>
    AudioOnly,
}

public static class VideoQualities
{
    public static string GetDisplayName(this VideoQuality quality) => quality switch
    {
        VideoQuality.Best => "Beste Qualität (MP4)",
        VideoQuality.Max1080 => "Bis 1080p (MP4)",
        VideoQuality.Max720 => "Bis 720p (MP4)",
        VideoQuality.Max480 => "Bis 480p (MP4)",
        VideoQuality.AudioOnly => "Nur Audio (M4A)",
        _ => quality.ToString(),
    };

    /// <summary>
    /// yt-dlp arguments that select the formats. H.264 video and AAC audio are preferred because
    /// they play everywhere; the streams are merged into an MP4 container.
    /// </summary>
    public static IReadOnlyList<string> GetFormatArguments(this VideoQuality quality)
    {
        if (quality == VideoQuality.AudioOnly)
            return ["-f", "ba[ext=m4a]/ba/b", "-x", "--audio-format", "m4a"];

        var resolution = quality switch
        {
            VideoQuality.Max1080 => "res:1080,",
            VideoQuality.Max720 => "res:720,",
            VideoQuality.Max480 => "res:480,",
            _ => "res,",
        };

        return
        [
            "-f", "bv*+ba/b",
            "-S", resolution + "vcodec:h264,acodec:m4a",
            "--merge-output-format", "mp4",
        ];
    }
}
