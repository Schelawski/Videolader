namespace Videolader.Core;

/// <summary>
/// Knows which videos already exist in the output folder. A video counts as downloaded if a media
/// file named after its ID exists (<c>dQw4w9WgXcQ.mp4</c>) or if its ID is listed in the archive file.
/// The archive keeps working when videos are moved elsewhere after downloading.
/// </summary>
public sealed class VideoLibrary
{
    /// <summary>Archive file in the output folder. Same format as yt-dlp's <c>--download-archive</c>.</summary>
    public const string ArchiveFileName = "videolader-archiv.txt";

    public static readonly IReadOnlySet<string> MediaExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4a", ".mkv", ".webm", ".mp3", ".opus", ".mov", ".m4v",
    };

    private readonly HashSet<string> _archive;

    private VideoLibrary(string folder, HashSet<string> archive)
    {
        Folder = folder;
        _archive = archive;
    }

    public string Folder { get; }

    public string ArchivePath => Path.Combine(Folder, ArchiveFileName);

    /// <summary>Reads the archive of <paramref name="folder"/>. The folder does not need to exist.</summary>
    public static VideoLibrary Open(string folder)
    {
        var archive = new HashSet<string>(StringComparer.Ordinal);
        var path = Path.Combine(folder, ArchiveFileName);
        if (File.Exists(path))
        {
            foreach (var line in File.ReadLines(path))
            {
                var id = ParseArchiveLine(line);
                if (id is not null)
                    archive.Add(id);
            }
        }

        return new VideoLibrary(folder, archive);
    }

    /// <summary>Accepts "youtube ID" (yt-dlp format) and a bare "ID".</summary>
    internal static string? ParseArchiveLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var id = parts.Length switch
        {
            1 => parts[0],
            2 when parts[0].Equals("youtube", StringComparison.OrdinalIgnoreCase) => parts[1],
            _ => null,
        };
        return id is not null && InputParser.IsVideoId(id) ? id : null;
    }

    public bool IsInArchive(string id) => _archive.Contains(id);

    public bool IsDownloaded(string id) => IsInArchive(id) || FindMediaFile(id) is not null;

    /// <summary>Returns the media file named after the ID, or null.</summary>
    public string? FindMediaFile(string id)
    {
        if (!Directory.Exists(Folder))
            return null;

        foreach (var file in Directory.EnumerateFiles(Folder, id + ".*"))
        {
            if (string.Equals(Path.GetFileNameWithoutExtension(file), id, StringComparison.Ordinal) &&
                MediaExtensions.Contains(Path.GetExtension(file)))
                return file;
        }

        return null;
    }

    /// <summary>Adds the ID to the archive file.</summary>
    public void MarkDownloaded(string id)
    {
        if (!_archive.Add(id))
            return;

        Directory.CreateDirectory(Folder);
        File.AppendAllText(ArchivePath, $"youtube {id}{Environment.NewLine}");
    }
}
