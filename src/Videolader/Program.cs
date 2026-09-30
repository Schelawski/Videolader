using Videolader.UI;

namespace Videolader;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Applies the settings from the project file (PerMonitorV2 high DPI, visual styles, default font).
        ApplicationConfiguration.Initialize();

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show("Unerwarteter Fehler:\n\n" + e.Exception.Message, MainForm.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new MainForm());
    }
}
