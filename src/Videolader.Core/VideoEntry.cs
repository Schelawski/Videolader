namespace Videolader.Core;

/// <summary>One video in the download list.</summary>
public sealed class VideoEntry
{
    public VideoEntry(string id) => Id = id;

    public string Id { get; }

    /// <summary>Title, or null while unknown.</summary>
    public string? Title { get; set; }

    public string? Channel { get; set; }

    public TimeSpan? Duration { get; set; }

    /// <summary>Private or deleted video (known before downloading).</summary>
    public bool Unavailable { get; set; }

    public string? PlaylistId { get; set; }

    public string? PlaylistTitle { get; set; }

    /// <summary>1-based position in the playlist.</summary>
    public int? PlaylistIndex { get; set; }

    public string Url => YouTubeUrls.Video(Id);

    /// <summary>Titles YouTube uses for entries that cannot be watched.</summary>
    public static bool IsUnavailableTitle(string? title) => title is
        "[Private video]" or "[Deleted video]" or "Private video" or "Deleted video" or
        "[Unavailable video]" or "Unavailable video";
}
