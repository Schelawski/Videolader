using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Videolader.Core;

/// <summary>Progress of a tool download.</summary>
public sealed record ToolDownloadProgress(string Tool, long Received, long? Total)
{
    public double? Percent => Total is > 0 ? 100.0 * Received / Total.Value : null;
}

/// <summary>
/// Downloads the Windows builds of yt-dlp, ffmpeg and Deno from their official GitHub releases. Every download is
/// checked against the SHA-256 checksum published in the same release before anything is extracted or started.
/// </summary>
public sealed partial class ToolInstaller(HttpClient http)
{
    public const string YtDlpUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    public const string YtDlpChecksumUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";

    // Static ffmpeg build maintained by the yt-dlp team (contains patches yt-dlp relies on).
    public const string FfmpegUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
    public const string FfmpegChecksumUrl = "https://github.com/yt-dlp/FFmpeg-Builds/releases/download/latest/checksums.sha256";

    public const string DenoUrl = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
    public const string DenoChecksumUrl = DenoUrl + ".sha256sum";

    public async Task InstallYtDlpAsync(string toolsDirectory, IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(toolsDirectory);
        var target = Path.Combine(toolsDirectory, "yt-dlp.exe");
        var temp = target + ".download";
        try
        {
            await DownloadVerifiedAsync("yt-dlp", YtDlpUrl, YtDlpChecksumUrl, temp, progress, ct).ConfigureAwait(false);
            File.Move(temp, target, overwrite: true);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    public Task InstallFfmpegAsync(string toolsDirectory, IProgress<ToolDownloadProgress>? progress, CancellationToken ct) =>
        InstallFromZipAsync("ffmpeg", FfmpegUrl, FfmpegChecksumUrl, ["ffmpeg.exe", "ffprobe.exe"], toolsDirectory, progress, ct);

    public Task InstallDenoAsync(string toolsDirectory, IProgress<ToolDownloadProgress>? progress, CancellationToken ct) =>
        InstallFromZipAsync("Deno", DenoUrl, DenoChecksumUrl, ["deno.exe"], toolsDirectory, progress, ct);

    private async Task InstallFromZipAsync(
        string tool, string url, string checksumUrl, string[] fileNames, string toolsDirectory,
        IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        Directory.CreateDirectory(toolsDirectory);
        var zipPath = Path.Combine(toolsDirectory, tool.ToLowerInvariant() + ".zip.download");
        try
        {
            await DownloadVerifiedAsync(tool, url, checksumUrl, zipPath, progress, ct).ConfigureAwait(false);
            ExtractFiles(zipPath, fileNames, toolsDirectory);
        }
        finally
        {
            TryDelete(zipPath);
        }
    }

    /// <summary>
    /// Reads the published checksum first (so a missing one stops before a large download), then downloads and
    /// compares. On a mismatch the file is deleted by the caller and nothing is installed.
    /// </summary>
    private async Task DownloadVerifiedAsync(
        string tool, string url, string checksumUrl, string targetPath, IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        var checksums = await http.GetStringAsync(checksumUrl, ct).ConfigureAwait(false);
        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        var expected = FindChecksum(checksums, fileName)
            ?? throw new InvalidDataException(UiText.ChecksumMissing(tool));

        var actual = await DownloadAsync(tool, url, targetPath, progress, ct).ConfigureAwait(false);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException(UiText.ChecksumMismatch(tool));
    }

    /// <summary>
    /// The SHA-256 of <paramref name="fileName"/> (lower case) in a published checksum file, or null. Understands the
    /// <c>sha256sum</c> format (<c>&lt;hash&gt;  &lt;file&gt;</c>, used by yt-dlp and FFmpeg-Builds) and the output of
    /// PowerShell's <c>Get-FileHash | Format-List</c> (<c>Hash : …</c> / <c>Path : …</c>, used by Deno).
    /// </summary>
    internal static string? FindChecksum(string text, string fileName)
    {
        var lines = text.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (SumLine().Match(line) is { Success: true } sum &&
                Path.GetFileName(sum.Groups["file"].Value.Trim()).Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                return sum.Groups["hash"].Value.ToLowerInvariant();
            }
        }

        // Get-FileHash: one file per block; the "Path" line names the file (a full path of the build machine).
        var hash = lines.Select(line => HashLine().Match(line)).FirstOrDefault(m => m.Success)?.Groups["hash"].Value;
        var path = lines.Select(line => PathLine().Match(line)).FirstOrDefault(m => m.Success)?.Groups["path"].Value;
        return hash is not null && path is not null && path.Replace('\\', '/').Split('/')[^1].Equals(fileName, StringComparison.OrdinalIgnoreCase)
            ? hash.ToLowerInvariant()
            : null;
    }

    [GeneratedRegex(@"^(?<hash>[0-9A-Fa-f]{64})\s+\*?(?<file>\S.*)$")]
    private static partial Regex SumLine();

    [GeneratedRegex(@"^Hash\s*:\s*(?<hash>[0-9A-Fa-f]{64})$")]
    private static partial Regex HashLine();

    [GeneratedRegex(@"^Path\s*:\s*(?<path>.+)$")]
    private static partial Regex PathLine();

    /// <summary>Extracts the named files from anywhere inside the zip into <paramref name="targetDirectory"/>.</summary>
    internal static void ExtractFiles(string zipPath, IReadOnlyCollection<string> fileNames, string targetDirectory)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var fileName in fileNames)
        {
            var entry = archive.Entries.FirstOrDefault(e => e.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(UiText.FileNotInArchive(fileName));

            var target = Path.Combine(targetDirectory, fileName);
            var temp = target + ".extract";
            entry.ExtractToFile(temp, overwrite: true);
            File.Move(temp, target, overwrite: true);
        }
    }

    /// <summary>Downloads to <paramref name="targetPath"/> and returns the SHA-256 of the file (lower case).</summary>
    private async Task<string> DownloadAsync(
        string tool, string url, string targetPath, IProgress<ToolDownloadProgress>? progress, CancellationToken ct)
    {
        using var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
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
                sha256.AppendData(buffer, 0, read);
                received += read;
                if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(200))
                {
                    lastReport = DateTime.UtcNow;
                    progress?.Report(new ToolDownloadProgress(tool, received, total));
                }
            }

            progress?.Report(new ToolDownloadProgress(tool, received, total));
        }

        return Convert.ToHexStringLower(sha256.GetHashAndReset());
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
