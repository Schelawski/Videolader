namespace Videolader.UI;

/// <summary>Colors and small control factories shared by the window (same look as Wortlaut).</summary>
internal static class UiStyle
{
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentDisabled = Color.FromArgb(191, 207, 240);
    public static readonly Color MutedText = Color.FromArgb(75, 85, 99);
    public static readonly Color GridLine = Color.FromArgb(229, 231, 235);

    public static readonly Color SuccessText = Color.FromArgb(22, 101, 52);
    public static readonly Color InfoText = Color.FromArgb(30, 64, 175);
    public static readonly Color WarningText = Color.FromArgb(146, 64, 14);
    public static readonly Color DangerText = Color.FromArgb(153, 27, 27);
    public static readonly Color DisabledText = Color.FromArgb(156, 163, 175);

    public static Font CreateMonospaceFont(float size = 9f) => new("Consolas", size);

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
