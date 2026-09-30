using System.Diagnostics;
using System.Text;

namespace Videolader.Core;

/// <summary>Starts console programs without a window and streams their output line by line.</summary>
public static class ProcessRunner
{
    /// <summary>
    /// Runs the program and reports every line of stdout and stderr to <paramref name="onLine"/>
    /// (called on a thread-pool thread). On cancellation the whole process tree is killed and
    /// <see cref="OperationCanceledException"/> is thrown.
    /// </summary>
    public static async Task<int> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        Action<string> onLine,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments, environment) };
        var sync = new object();
        void Report(string? line)
        {
            if (line is null)
                return;
            lock (sync)
                onLine(line);
        }

        process.Start();
        using var registration = cancellationToken.Register(() => TryKill(process));

        var stdout = PumpAsync(process.StandardOutput, Report);
        var stderr = PumpAsync(process.StandardError, Report);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return process.ExitCode;
    }

    /// <summary>Runs the program and returns its complete output.</summary>
    public static async Task<(int ExitCode, string StdOut, string StdErr)> CaptureAsync(
        string fileName,
        IEnumerable<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = new Process { StartInfo = CreateStartInfo(fileName, arguments, environment) };
        process.Start();
        using var registration = cancellationToken.Register(() => TryKill(process));

        var stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
        await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
        return (process.ExitCode, stdout.Result, stderr.Result);
    }

    private static ProcessStartInfo CreateStartInfo(
        string fileName, IEnumerable<string> arguments, IReadOnlyDictionary<string, string>? environment)
    {
        var info = new ProcessStartInfo(fileName)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(fileName)) ?? Environment.CurrentDirectory,
        };

        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                info.Environment[key] = value;
        }

        return info;
    }

    private static async Task PumpAsync(StreamReader reader, Action<string?> report)
    {
        string? line;
        while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) is not null)
            report(line);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        {
            // Already gone.
        }
    }
}
