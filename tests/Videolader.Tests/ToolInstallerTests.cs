using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Videolader.Core;

namespace Videolader.Tests;

public class ChecksumFileTests
{
    // Excerpts of the real files (25.10.2026).
    private const string YtDlpSums = """
        3ad0c2b2d3a0b6f3fd5a6b0b0d0a1b0f6c8e4d9e1f2a3b4c5d6e7f8091a2b3c4  yt-dlp
        33121d192d706e62f684a06f80918e3f3a8ba52ac3a65213b6951c39c7d9f555  yt-dlp.exe
        0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0  yt-dlp_x86.exe
        """;

    private const string FfmpegSums = """
        1111111111111111111111111111111111111111111111111111111111111111  ffmpeg-master-latest-linux64-gpl.tar.xz
        7d7dda0e6db822d2a3587f9103f77a9701d6c5642ab15dbb12a5d2db5aefcc4b  ffmpeg-master-latest-win64-gpl.zip
        2222222222222222222222222222222222222222222222222222222222222222  ffmpeg-master-latest-win64-gpl-shared.zip
        """;

    // Deno publishes the output of PowerShell's Get-FileHash | Format-List (CRLF, blank lines around it).
    private const string DenoSum =
        "\r\nAlgorithm : SHA256\r\nHash      : CBA84B26E0DF5AE7F74FC66B86A95B866872A86292EBD84FA1E959927EDD93BF\r\n" +
        "Path      : C:\\a\\deno\\deno\\target\\release\\deno-x86_64-pc-windows-msvc.zip\r\n\r\n";

    [Fact]
    public void YtDlpChecksumIsFoundByExactFileName()
    {
        Assert.Equal("33121d192d706e62f684a06f80918e3f3a8ba52ac3a65213b6951c39c7d9f555", ToolInstaller.FindChecksum(YtDlpSums, "yt-dlp.exe"));
    }

    [Fact]
    public void FfmpegChecksumIsNotConfusedWithSimilarNames()
    {
        Assert.Equal("7d7dda0e6db822d2a3587f9103f77a9701d6c5642ab15dbb12a5d2db5aefcc4b",
            ToolInstaller.FindChecksum(FfmpegSums, "ffmpeg-master-latest-win64-gpl.zip"));
    }

    [Fact]
    public void DenoPowerShellFormatIsUnderstoodAndLowerCased()
    {
        Assert.Equal("cba84b26e0df5ae7f74fc66b86a95b866872a86292ebd84fa1e959927edd93bf",
            ToolInstaller.FindChecksum(DenoSum, "deno-x86_64-pc-windows-msvc.zip"));
    }

    [Theory]
    [InlineData("yt-dlp_macos")]
    [InlineData("ffmpeg.zip")]
    public void UnknownFileGivesNoChecksum(string fileName)
    {
        Assert.Null(ToolInstaller.FindChecksum(YtDlpSums + FfmpegSums, fileName));
        Assert.Null(ToolInstaller.FindChecksum(DenoSum, fileName));
    }

    [Fact]
    public void BinaryMarkerOfSha256SumIsAccepted()
    {
        Assert.Equal(new string('a', 64), ToolInstaller.FindChecksum(new string('A', 64) + " *tool.exe", "tool.exe"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("<html>Not Found</html>")]
    [InlineData("abc  yt-dlp.exe")]
    public void BrokenFilesGiveNoChecksum(string text)
    {
        Assert.Null(ToolInstaller.FindChecksum(text, "yt-dlp.exe"));
    }
}

/// <summary>Installing with a fake GitHub: the published checksum decides whether anything is installed.</summary>
public class ToolInstallerTests
{
    private static readonly byte[] FakeExe = Encoding.ASCII.GetBytes("MZ fake yt-dlp");

    [Fact]
    public async Task MatchingChecksumInstallsTheTool()
    {
        using var folder = new TempFolder();
        var server = new FakeGitHub()
            .With(ToolInstaller.YtDlpChecksumUrl, $"{Sha256(FakeExe)}  yt-dlp.exe\n")
            .With(ToolInstaller.YtDlpUrl, FakeExe);

        await new ToolInstaller(new HttpClient(server)).InstallYtDlpAsync(folder.Path, null, CancellationToken.None);

        Assert.Equal(FakeExe, File.ReadAllBytes(Path.Combine(folder.Path, "yt-dlp.exe")));
        Assert.Single(Directory.GetFiles(folder.Path));
    }

    [Fact]
    public async Task WrongChecksumInstallsNothingAndLeavesNoFile()
    {
        using var folder = new TempFolder();
        var server = new FakeGitHub()
            .With(ToolInstaller.YtDlpChecksumUrl, $"{new string('0', 64)}  yt-dlp.exe\n")
            .With(ToolInstaller.YtDlpUrl, FakeExe);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ToolInstaller(new HttpClient(server)).InstallYtDlpAsync(folder.Path, null, CancellationToken.None));

        Assert.Empty(Directory.GetFiles(folder.Path));
    }

    [Fact]
    public async Task MissingChecksumStopsBeforeTheDownload()
    {
        using var folder = new TempFolder();
        var server = new FakeGitHub()
            .With(ToolInstaller.FfmpegChecksumUrl, $"{new string('1', 64)}  something-else.zip\n")
            .With(ToolInstaller.FfmpegUrl, Zip(("bin/ffmpeg.exe", FakeExe), ("bin/ffprobe.exe", FakeExe)));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ToolInstaller(new HttpClient(server)).InstallFfmpegAsync(folder.Path, null, CancellationToken.None));

        Assert.DoesNotContain(ToolInstaller.FfmpegUrl, server.Requested);
        Assert.Empty(Directory.GetFiles(folder.Path));
    }

    [Fact]
    public async Task VerifiedZipIsExtracted()
    {
        using var folder = new TempFolder();
        var zip = Zip(("deno.exe", FakeExe));
        var server = new FakeGitHub()
            .With(ToolInstaller.DenoChecksumUrl, $"Algorithm : SHA256\r\nHash      : {Sha256(zip).ToUpperInvariant()}\r\nPath      : D:\\a\\deno-x86_64-pc-windows-msvc.zip\r\n")
            .With(ToolInstaller.DenoUrl, zip);

        await new ToolInstaller(new HttpClient(server)).InstallDenoAsync(folder.Path, null, CancellationToken.None);

        Assert.Equal(FakeExe, File.ReadAllBytes(Path.Combine(folder.Path, "deno.exe")));
        Assert.Single(Directory.GetFiles(folder.Path));
    }

    private static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    private static byte[] Zip(params (string Name, byte[] Data)[] files)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, data) in files)
            {
                using var entry = archive.CreateEntry(name).Open();
                entry.Write(data);
            }
        }

        return memory.ToArray();
    }

    private sealed class FakeGitHub : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses = [];

        public List<string> Requested { get; } = [];

        public FakeGitHub With(string url, string text) => With(url, Encoding.UTF8.GetBytes(text));

        public FakeGitHub With(string url, byte[] data)
        {
            _responses[url] = data;
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requested.Add(url);
            return Task.FromResult(_responses.TryGetValue(url, out var data)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
