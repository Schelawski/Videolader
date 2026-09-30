namespace Videolader.Core;

/// <summary>
/// Paths of the external programs. yt-dlp is required, ffmpeg merges video and audio,
/// Deno solves YouTube's JavaScript challenges (without it many formats are missing).
/// </summary>
public sealed record ToolSet(string ToolsDirectory, string? YtDlp, string? Ffmpeg, string? Deno)
{
    public bool IsComplete => YtDlp is not null && Ffmpeg is not null && Deno is not null;

    /// <summary>
    /// Environment for yt-dlp: the tools folder comes first on PATH so yt-dlp finds ffmpeg and Deno,
    /// and Python is told to write UTF-8 (Cyrillic titles in the log).
    /// </summary>
    public IReadOnlyDictionary<string, string> CreateEnvironment()
    {
        var directories = new[] { ToolsDirectory, DirectoryOf(Ffmpeg), DirectoryOf(Deno) }
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        var path = string.Join(Path.PathSeparator, directories.Append(Environment.GetEnvironmentVariable("PATH") ?? string.Empty));

        return new Dictionary<string, string>
        {
            ["PATH"] = path,
            ["PYTHONIOENCODING"] = "utf-8",
            ["PYTHONUTF8"] = "1",
        };
    }

    private static string? DirectoryOf(string? file) => file is null ? null : Path.GetDirectoryName(file);
}

/// <summary>Finds the external programs.</summary>
public static class ToolLocator
{
    public const string ToolsFolderName = "tools";

    public static string ExecutableName(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    /// <summary>
    /// The folder downloaded tools are stored in: <c>tools</c> next to the executable, or
    /// <c>%LOCALAPPDATA%\Videolader\tools</c> if that folder is not writable.
    /// </summary>
    public static string GetToolsDirectory(string appDirectory)
    {
        var local = Path.Combine(appDirectory, ToolsFolderName);
        if (IsWritable(local))
            return local;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Videolader", ToolsFolderName);
    }

    /// <summary>Looks in the tools folder, next to the executable and on PATH.</summary>
    public static ToolSet Locate(string toolsDirectory, string appDirectory)
    {
        string[] folders = [toolsDirectory, appDirectory];
        return new ToolSet(
            toolsDirectory,
            Find("yt-dlp", folders),
            Find("ffmpeg", folders),
            Find("deno", folders));
    }

    private static string? Find(string name, IEnumerable<string> folders)
    {
        var fileName = ExecutableName(name);
        var pathFolders = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var folder in folders.Concat(pathFolders))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim('"'), fileName);
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }
            catch (ArgumentException)
            {
                // Invalid entry on PATH.
            }
        }

        return null;
    }

    private static bool IsWritable(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".write-test");
            File.WriteAllText(probe, string.Empty);
            File.Delete(probe);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
