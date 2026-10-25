namespace Videolader.UI;

/// <summary>Colors and small control factories shared by the windows (layout as in Wortlaut, colors from the red app icon).</summary>
internal static class UiStyle
{
    /// <summary>Cinema red between the two colors of the app icon (#E8424F → #A8173A); white text on it stays readable.</summary>
    public static readonly Color Accent = Color.FromArgb(198, 40, 64);
    public static readonly Color AccentDisabled = Color.FromArgb(236, 190, 198);

    /// <summary>Background of selected rows in the video list (instead of the Windows blue).</summary>
    public static readonly Color Selection = Color.FromArgb(252, 228, 232);
    public static readonly Color MutedText = Color.FromArgb(75, 85, 99);
    public static readonly Color GridLine = Color.FromArgb(229, 231, 235);

    public static readonly Color SuccessText = Color.FromArgb(22, 101, 52);
    public static readonly Color InfoText = Color.FromArgb(30, 64, 175);
    public static readonly Color WarningText = Color.FromArgb(146, 64, 14);
    public static readonly Color DangerText = Color.FromArgb(153, 27, 27);
    public static readonly Color DisabledText = Color.FromArgb(156, 163, 175);

    /// <summary>Background and text color of a small round badge.</summary>
    public readonly record struct Badge(Color Back, Color Fore);

    /// <summary>The "?" next to the settings: light rose with the dark red of the icon.</summary>
    public static readonly Badge Info = new(Color.FromArgb(252, 228, 232), Color.FromArgb(168, 23, 58));

    /// <summary>The icon embedded in Videolader.exe (see ApplicationIcon in the project), for the window title bars.</summary>
    public static Icon? AppIcon { get; } = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);

    public static Font CreateMonospaceFont(float size = 9f) => new("Consolas", size);

    public static void FillRoundedRectangle(Graphics graphics, Color color, RectangleF bounds, float radius)
    {
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        using var brush = new SolidBrush(color);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.FillPath(brush, path);
    }

    public static void MakePrimary(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.ForeColor = Color.White;
        button.Font = new Font(button.Font, FontStyle.Bold);
        button.UseVisualStyleBackColor = false;

        void Apply() => button.BackColor = button.Enabled ? Accent : AccentDisabled;
        button.EnabledChanged += (_, _) => Apply();
        Apply();
    }

    public static Button CreateButton(string text) => new()
    {
        Text = text,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Padding = new Padding(10, 3, 10, 3),
        MinimumSize = new Size(0, 30),
        UseVisualStyleBackColor = true,
    };

    public static Label CreateLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        UseMnemonic = false,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 6, 6, 3),
    };

    public static Label CreateHint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        UseMnemonic = false,
        ForeColor = MutedText,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(3, 6, 3, 3),
    };

    /// <summary>Windows' own icon font (Windows 11, else Windows 10); null if neither is installed.</summary>
    private static readonly string? IconFontName = new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }.FirstOrDefault(name =>
    {
        using var font = new Font(name, 10f);
        return font.Name == name;
    });

    /// <summary>Draws a glyph of the icon font as a 16 px image (scaled to <paramref name="dpi"/>); null without icon font.</summary>
    public static Bitmap? CreateIcon(char glyph, Color color, int dpi)
    {
        if (IconFontName is null)
            return null;

        var size = (int)Math.Round(16 * dpi / 96f);
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font(IconFontName, size * 0.85f, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(color);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(glyph.ToString(), font, brush, new RectangleF(0, 0, size, size), format);
        return bitmap;
    }

    /// <summary>Puts an icon in front of the button text, in the button's text color.</summary>
    public static void SetIcon(Button button, char glyph, int dpi)
    {
        if (CreateIcon(glyph, button.ForeColor, dpi) is not { } icon)
            return;

        var old = button.Image;
        button.Image = icon;
        button.TextImageRelation = TextImageRelation.ImageBeforeText;
        old?.Dispose();
    }
}

/// <summary>Code points of the icon font (identical in Segoe Fluent Icons and Segoe MDL2 Assets).</summary>
internal static class Glyph
{
    public const char Cancel = '';
    public const char ChevronDown = '';
    public const char ChevronUp = '';
    public const char Settings = '';
    public const char Filter = '';
    public const char Refresh = '';
    public const char Checkbox = '';
    public const char CheckboxChecked = '';
    public const char Delete = '';
    public const char Warning = '';
    public const char Download = '';
    public const char Help = '';
    public const char Folder = '';
    public const char List = '';
    public const char OpenFolder = '';
}

/// <summary>Read-only, monospaced log that scrolls to the newest line.</summary>
internal sealed class LogBox : TextBox
{
    private const int MaxChars = 1_000_000;

    public LogBox()
    {
        Multiline = true;
        ReadOnly = true;
        WordWrap = true;
        ScrollBars = ScrollBars.Vertical;
        MaxLength = int.MaxValue;
        Font = UiStyle.CreateMonospaceFont();
        BackColor = Color.FromArgb(243, 244, 246);
        Dock = DockStyle.Fill;
    }

    public void AppendLine(string line)
    {
        if (TextLength > MaxChars)
        {
            var text = Text;
            var cut = text.IndexOf('\n', text.Length - MaxChars / 2);
            Text = cut < 0 ? string.Empty : text[(cut + 1)..];
        }

        AppendText(line + Environment.NewLine);
    }

    /// <summary>Appends a Videolader message with a time stamp.</summary>
    public void AppendMessage(string message) => AppendLine($"[{DateTime.Now:HH:mm:ss}] {message}");
}
