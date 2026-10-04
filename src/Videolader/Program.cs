using Videolader.UI;

namespace Videolader;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // With arguments Videolader runs on the command line; without them the window opens.
        if (args.Length > 0)
            return CommandLine.Run(args);

        // Applies the settings from the project file (PerMonitorV2 high DPI, visual styles, default font).
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show("Unerwarteter Fehler:\n\n" + e.Exception.Message, MainForm.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new MainForm());
        return 0;
    }
}
