using System.Drawing.Drawing2D;
using Videolader.Core;

namespace Videolader.UI;

/// <summary>
/// Small round "?" next to a setting: the tooltip explains it in one or two sentences, a click opens the
/// matching help topic.
/// </summary>
internal sealed class HelpBadge : Control
{
    private bool _hover;

    /// <param name="topicId">Help topic opened on click (see <see cref="Core.HelpTopics"/>).</param>
    /// <param name="tip">Short explanation shown as tooltip.</param>
    /// <param name="toolTip">The window's tooltip component.</param>
    public HelpBadge(string topicId, string tip, ToolTip toolTip)
    {
        TopicId = topicId;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        Size = new Size(16, 16);
        Margin = new Padding(0, 7, 0, 0); // level with the label next to it (see UiStyle.CreateLabel)
        AccessibleName = "?";
        AccessibleDescription = tip;
        AccessibleRole = AccessibleRole.HelpBalloon;
        toolTip.SetToolTip(this, tip + Environment.NewLine + UiText.HelpClickForMore);
    }

    public string TopicId { get; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (FindForm() is { } form)
            HelpForm.Open(form, TopicId);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var circle = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        var badge = _hover ? new UiStyle.Badge(UiStyle.Accent, Color.White) : UiStyle.Info;
        using (var fill = new SolidBrush(badge.Back))
            e.Graphics.FillEllipse(fill, circle);

        using var font = new Font(Font.FontFamily, 7.5f, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, "?", font, ClientRectangle, badge.Fore,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
