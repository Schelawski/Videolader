using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Videolader.Core;

namespace Videolader;

/// <summary>
/// Command line mode of Videolader.exe. The exe is a Windows application without a console of its own,
/// so it attaches to the console it was started from. If the output is redirected (file, pipe) the
/// redirection is used as it is.
/// </summary>
internal static class CommandLine
{
    private const int StdOutput = -11;
    private const int StdError = -12;
    private const int AttachParentProcess = -1;

    public static int Run(string[] args)
    {
        ConnectConsole();

        var parsed = CliArguments.Parse(args);
        if (parsed.Options is null)
        {
            Console.Error.WriteLine(parsed.Error);
            return CliRunner.ExitBadInput;
        }

        if (parsed.Options.Help)
        {
            Console.Out.WriteLine(CliArguments.HelpText);
            return CliRunner.ExitOk;
        }

        using var cancel = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancel.Cancel();
        };

        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Videolader", "1.0"));

        // The API key is stored encrypted; loading needs the Windows protector. Nothing is saved from here.
        var settings = new SettingsStore(new DpapiSecretProtector()).Load();
        var runner = new CliRunner(
            http,
            settings,
            ToolLocator.GetToolsDirectory(AppContext.BaseDirectory),
            AppContext.BaseDirectory,
            Console.Out,
            Console.Error);

        return runner.RunAsync(parsed.Options, cancel.Token).GetAwaiter().GetResult();
    }

    private static void ConnectConsole()
    {
        // A handle that is already there means the output is redirected: leave it alone.
        var needStdOut = !IsValid(GetStdHandle(StdOutput));
        var needStdErr = !IsValid(GetStdHandle(StdError));

        if (needStdOut || needStdErr)
        {
            AttachConsole(AttachParentProcess);
            if (needStdOut)
                Redirect(StdOutput);
            if (needStdErr)
                Redirect(StdError);
        }

        try
        {
            var utf8 = new UTF8Encoding(false);
            Console.OutputEncoding = utf8;
        }
        catch (IOException)
        {
            // No console (started by a scheduler, output discarded): nothing to set.
        }
    }

    private static void Redirect(int standardHandle)
    {
        var file = CreateFile("CONOUT$", 0x40000000 /* GENERIC_WRITE */, 2 /* FILE_SHARE_WRITE */, IntPtr.Zero, 3 /* OPEN_EXISTING */, 0, IntPtr.Zero);
        if (!file.IsInvalid)
            SetStdHandle(standardHandle, file.DangerousGetHandle());
    }

    private static bool IsValid(IntPtr handle) => handle != IntPtr.Zero && handle != new IntPtr(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int standardHandle, IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
}
