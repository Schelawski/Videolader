using System.IO.Compression;

namespace Videolader.Core;

/// <summary>Progress of a tool download.</summary>
public sealed record ToolDownloadProgress(string Tool, long Received, long? Total)
{
    public double? Percent => Total is > 0 ? 100.0 * Received / Total.Value : null;
}

/// <summary>
/// Downloads the Windows builds of yt-dlp, ffmpeg and Deno from their official GitHub releases.
/// </summary>
public sealed class ToolInstaller(HttpClient http)
{
    public const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";

    // Static ffmpeg build maintained by the yt-dlp team (contains patches yt-dlp relies on).
    public const string FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";

    public const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

    public async Task InstallYtDlpAsync(string toolsDirectory, IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(toolsDirectory);
        var target = Path.Combine(toolsDirectory, "yt-dlp.exe");
        var temp = target + ".download";
        await DownloadAsync("yt-dlp", YtDlpUrl, temp, progress, ct).ConfigureAwait(false);
        File.Move(temp, target, overwrite: true);
    }

    public Task InstallFfmpegAsync(string toolsDirectory, IProgress<ToolDownloadProgress>? progress, CancellationToken ct) =>
        InstallFromZipAsync("ffmpeg", FfmpegUrl, ["ffmpeg.exe", "ffprobe.exe"], toolsDirectory, progress, ct);

    public Task InstallDenoAsync(string toolsDirectory, IProgress<ToolDownloadProgress>? progress, CancellationToken ct) =>
        InstallFromZipAsync("Deno", DenoUrl, ["deno.exe"], toolsDirectory, progress, ct);

    private async Task InstallFromZipAsync(
        string tool, string url, string[] fileNames, string toolsDirectory,
        IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(toolsDirectory);
        var zipPath = Path.Combine(toolsDirectory, tool.ToLowerInvariant() + ".zip.download");
        try
        {
            await DownloadAsync(tool, url, zipPath, progress, ct).ConfigureAwait(false);
            ExtractFiles(zipPath, fileNames, toolsDirectory);
        }
        finally
        {
            TryDelete(zipPath);
        }
    }

    /// <summary>Extracts the named files from anywhere inside the zip into <paramref name="targetDirectory"/>.</summary>
    internal static void ExtractFiles(string zipPath, IReadOnlyCollection<string> fileNames, string targetDirectory)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var fileName in fileNames)
        {
            var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{fileName} ist nicht im heruntergeladenen Archiv enthalten.");

            var target = Path.Combine(targetDirectory, fileName);
            var temp = target + ".extract";
            entry.ExtractToFile(temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }
    }

    private async Task DownloadAsync(
        string tool, string url, string targetPath, IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using (var target = File.Create(targetPath))
        {
            var buffer = new byte[81920];
            long received = 0;
            var lastReport = DateTime.MinValue;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                received += read;
                if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(200))
                {
                    lastReport = DateTime.UtcNow;
                    progress?.Report(new ToolDownloadProgress(tool, received, total));
                }
            }

            progress?.Report(new ToolDownloadProgress(tool, received, total));
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
