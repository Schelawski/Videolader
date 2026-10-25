using System.Globalization;
using Videolader.Core;
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
            MessageBox.Show(UiText.UnexpectedError(e.Exception.Message), MainForm.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);

        var windowsCulture = CultureInfo.CurrentUICulture;
        var store = new SettingsStore(new DpapiSecretProtector());
        var settings = store.Load();
        Rectangle? bounds = null;

        // Switching the language closes the window; it is then rebuilt with the new texts.
        while (true)
        {
            UiLanguages.Apply(UiLanguages.Resolve(settings.UiLanguage, windowsCulture));
            using var form = new MainForm(store, settings, bounds);
            Application.Run(form);

            if (!form.LanguageChangeRequested)
                break;
            bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        }

        return 0;
    }
}
