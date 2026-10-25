using System.Reflection;
using System.Text;

namespace Videolader.Core;

/// <summary>Ids of the help topics. All languages must contain exactly these topics in this order.</summary>
public static class HelpTopics
{
    public const string About = "about";
    public const string FirstSteps = "start";
    public const string Existing = "existing";
    public const string Quality = "quality";
    public const string Pause = "pause";
    public const string Cookies = "cookies";
    public const string ApiKey = "apikey";
    public const string Tools = "tools";
    public const string CommandLine = "cmdline";
    public const string Problems = "problems";
    public const string Uninstall = "uninstall";
    public const string Licenses = "licenses";

    public static IReadOnlyList<string> All { get; } =
        [About, FirstSteps, Existing, Quality, Pause, Cookies, ApiKey, Tools, CommandLine, Problems, Uninstall, Licenses];
}

/// <summary>Formatting of a piece of text.</summary>
public enum HelpSpanStyle
{
    Normal,

    /// <summary><c>**bold**</c></summary>
    Bold,

    /// <summary><c>`code`</c>, e.g. a folder path.</summary>
    Code,
}

public readonly record struct HelpSpan(string Text, HelpSpanStyle Style);

public abstract record HelpBlock(IReadOnlyList<HelpSpan> Spans);

/// <summary>A paragraph of running text.</summary>
public sealed record HelpParagraph(IReadOnlyList<HelpSpan> Spans) : HelpBlock(Spans);

/// <summary><c>## Subheading</c> inside a topic.</summary>
public sealed record HelpHeading(IReadOnlyList<HelpSpan> Spans) : HelpBlock(Spans);

/// <summary>A list item: <c>- item</c> (Marker "•") or <c>1. item</c> (Marker "1.").</summary>
public sealed record HelpListItem(string Marker, IReadOnlyList<HelpSpan> Spans) : HelpBlock(Spans);

public sealed record HelpTopic(string Id, string Title, IReadOnlyList<HelpBlock> Blocks);

/// <summary>
/// The help texts of one language. They are written in a small subset of Markdown and embedded into Videolader
/// (<c>Help/help.en.md</c>, <c>Help/help.de.md</c>, <c>Help/help.ru.md</c>), so they can be read and edited like plain text:
/// <list type="bullet">
/// <item><c># id | Title</c> starts a topic (id from <see cref="HelpTopics"/>).</item>
/// <item><c>## Text</c> is a subheading; <c>- </c> and <c>1. </c> start list items.</item>
/// <item>Lines are joined into paragraphs until an empty line; <c>**bold**</c> and <c>`code`</c> inside the text.</item>
/// </list>
/// </summary>
public sealed class HelpDocument
{
    private HelpDocument(IReadOnlyList<HelpTopic> topics) => Topics = topics;

    public IReadOnlyList<HelpTopic> Topics { get; }

    public HelpTopic? Find(string? id) =>
        Topics.FirstOrDefault(topic => string.Equals(topic.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Loads the embedded help of a language ("en", "de" or "ru").</summary>
    /// <exception cref="InvalidOperationException">The resource is missing (a build error).</exception>
    public static HelpDocument Load(string languageCode)
    {
        var name = $"Videolader.Help.help.{languageCode}.md";
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Help resource {name} not found.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd());
    }

    public static HelpDocument Parse(string markdown)
    {
        var topics = new List<HelpTopic>();
        string? id = null, title = null;
        var blocks = new List<HelpBlock>();
        var pending = new StringBuilder();
        string? pendingMarker = null; // null: paragraph, otherwise the list marker

        void FlushBlock()
        {
            if (pending.Length == 0)
                return;

            var spans = ParseInline(pending.ToString());
            blocks.Add(pendingMarker is null ? new HelpParagraph(spans) : new HelpListItem(pendingMarker, spans));
            pending.Clear();
            pendingMarker = null;
        }

        void FlushTopic()
        {
            FlushBlock();
            if (id is not null)
                topics.Add(new HelpTopic(id, title!, blocks.ToList()));
            blocks.Clear();
        }

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushTopic();
                var header = line[2..];
                var separator = header.IndexOf('|');
                id = separator < 0 ? header.Trim() : header[..separator].Trim();
                title = separator < 0 ? id : header[(separator + 1)..].Trim();
                continue;
            }

            if (id is null)
                continue; // text before the first topic (e.g. an editor's note)

            if (trimmed.Length == 0)
            {
                FlushBlock();
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushBlock();
                blocks.Add(new HelpHeading(ParseInline(line[3..].Trim())));
                continue;
            }

            if (ListMarker(trimmed) is { } marker)
            {
                FlushBlock();
                pendingMarker = marker.Marker;
                pending.Append(trimmed[marker.Length..].Trim());
                continue;
            }

            // Continuation of the current paragraph or list item.
            if (pending.Length > 0)
                pending.Append(' ');
            pending.Append(trimmed);
        }

        FlushTopic();
        return new HelpDocument(topics);
    }

    /// <summary>"- text" → "•", "12. text" → "12.".</summary>
    private static (string Marker, int Length)? ListMarker(string line)
    {
        if (line.StartsWith("- ", StringComparison.Ordinal))
            return ("•", 2);

        var digits = 0;
        while (digits < line.Length && char.IsAsciiDigit(line[digits]))
            digits++;
        return digits > 0 && digits + 1 < line.Length && line[digits] == '.' && line[digits + 1] == ' '
            ? (line[..(digits + 1)], digits + 2)
            : null;
    }

    /// <summary>Splits <c>**bold**</c> and <c>`code`</c> from normal text. An unclosed marker is kept as text.</summary>
    public static IReadOnlyList<HelpSpan> ParseInline(string text)
    {
        var spans = new List<HelpSpan>();
        var normal = new StringBuilder();
        var i = 0;

        void FlushNormal()
        {
            if (normal.Length > 0)
                spans.Add(new HelpSpan(normal.ToString(), HelpSpanStyle.Normal));
            normal.Clear();
        }

        while (i < text.Length)
        {
            if (text.AsSpan(i).StartsWith("**") && text.IndexOf("**", i + 2, StringComparison.Ordinal) is var end and > 0)
            {
                FlushNormal();
                spans.Add(new HelpSpan(text[(i + 2)..end], HelpSpanStyle.Bold));
                i = end + 2;
            }
            else if (text[i] == '`' && text.IndexOf('`', i + 1) is var close and > 0)
            {
                FlushNormal();
                spans.Add(new HelpSpan(text[(i + 1)..close], HelpSpanStyle.Code));
                i = close + 1;
            }
            else
            {
                normal.Append(text[i]);
                i++;
            }
        }

        FlushNormal();
        return spans;
    }
}
