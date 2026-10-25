using Videolader.Core;

namespace Videolader.Tests;

public class HelpDocumentTests
{
    [Fact]
    public void TopicsParagraphsAndListsAreParsed()
    {
        var document = HelpDocument.Parse("""
            Note for editors, ignored.

            # about | Was macht Videolader?

            Erste Zeile
            zweite Zeile.

            ## Unterpunkt
            - eins
              weiter
            - zwei
            12. zwölf

            # start | Erste Schritte
            Text.
            """);

        Assert.Equal(["about", "start"], document.Topics.Select(t => t.Id));
        var about = document.Topics[0];
        Assert.Equal("Was macht Videolader?", about.Title);
        Assert.Collection(
            about.Blocks,
            block => Assert.Equal("Erste Zeile zweite Zeile.", Assert.IsType<HelpParagraph>(block).Spans.Single().Text),
            block => Assert.Equal("Unterpunkt", Assert.IsType<HelpHeading>(block).Spans.Single().Text),
            block => Assert.Equal(("•", "eins weiter"), Item(block)),
            block => Assert.Equal(("•", "zwei"), Item(block)),
            block => Assert.Equal(("12.", "zwölf"), Item(block)));
        Assert.Equal("Text.", Assert.Single(document.Topics[1].Blocks).Spans.Single().Text);
    }

    private static (string, string) Item(HelpBlock block)
    {
        var item = Assert.IsType<HelpListItem>(block);
        return (item.Marker, string.Concat(item.Spans.Select(s => s.Text)));
    }

    [Fact]
    public void BoldAndCodeAreRecognized()
    {
        var spans = HelpDocument.ParseInline("Ordner `%LOCALAPPDATA%\\Videolader` **löschen**, fertig.");

        Assert.Equal(
            [
                new HelpSpan("Ordner ", HelpSpanStyle.Normal),
                new HelpSpan("%LOCALAPPDATA%\\Videolader", HelpSpanStyle.Code),
                new HelpSpan(" ", HelpSpanStyle.Normal),
                new HelpSpan("löschen", HelpSpanStyle.Bold),
                new HelpSpan(", fertig.", HelpSpanStyle.Normal),
            ],
            spans);
    }

    [Theory]
    [InlineData("2 ** 3")]
    [InlineData("a `b")]
    public void UnclosedMarkersStayText(string text)
    {
        var span = Assert.Single(HelpDocument.ParseInline(text));
        Assert.Equal(new HelpSpan(text, HelpSpanStyle.Normal), span);
    }

    [Fact]
    public void TopicWithoutTitleUsesItsId()
    {
        Assert.Equal("about", HelpDocument.Parse("# about\nText").Topics.Single().Title);
    }

    [Fact]
    public void FindIgnoresCaseAndUnknownIds()
    {
        var document = HelpDocument.Parse("# about | A\nx\n# start | B\ny");

        Assert.Equal("B", document.Find("START")?.Title);
        Assert.Null(document.Find("nope"));
        Assert.Null(document.Find(null));
    }

    // ----- The real help texts -----

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("ru")]
    public void EmbeddedHelpHasAllTopicsInOrder(string language)
    {
        var document = HelpDocument.Load(language);

        Assert.Equal(HelpTopics.All, document.Topics.Select(t => t.Id));
        Assert.All(document.Topics, topic =>
        {
            Assert.False(string.IsNullOrWhiteSpace(topic.Title));
            Assert.True(topic.Blocks.Count >= 2, $"Topic {topic.Id} is (almost) empty.");
        });
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    [InlineData("ru")]
    public void EmbeddedHelpHasNoBrokenMarkup(string language)
    {
        var texts = HelpDocument.Load(language).Topics
            .SelectMany(topic => topic.Blocks.Prepend(new HelpHeading([new HelpSpan(topic.Title, HelpSpanStyle.Normal)])))
            .SelectMany(block => block.Spans)
            .Select(span => span.Text);

        Assert.All(texts, text =>
        {
            Assert.DoesNotContain("**", text);
            Assert.DoesNotContain("`", text);
            Assert.False(text.TrimStart().StartsWith('#'), $"Stray heading: {text}");
        });
    }

    [Fact]
    public void EachHelpIsInItsLanguage()
    {
        static bool Cyrillic(string text) => text.Any(c => c is >= 'Ѐ' and <= 'ӿ');
        static string AllText(HelpTopic topic) => topic.Title + string.Concat(topic.Blocks.SelectMany(b => b.Spans).Select(s => s.Text));

        var german = HelpDocument.Load("de");
        var russian = HelpDocument.Load("ru");
        var english = HelpDocument.Load("en");

        Assert.All(german.Topics, topic => Assert.False(Cyrillic(AllText(topic)), topic.Id));
        Assert.All(russian.Topics, topic => Assert.True(Cyrillic(topic.Title), topic.Id));
        Assert.All(english.Topics, topic =>
        {
            Assert.False(Cyrillic(AllText(topic)), topic.Id);
            Assert.False(AllText(topic).Any(c => "äöüÄÖÜß„".Contains(c)), $"English topic {topic.Id} looks German.");
        });

        // The translations cover the same ground: the same number of paragraphs and list items per topic.
        Assert.Equal(german.Topics.Select(t => t.Blocks.Count), russian.Topics.Select(t => t.Blocks.Count));
        Assert.Equal(german.Topics.Select(t => t.Blocks.Count), english.Topics.Select(t => t.Blocks.Count));
    }
}

/// <summary>The help names buttons, options and states; these names must match the user interface.</summary>
[Collection(nameof(UiTextCollection))]
public sealed class HelpMatchesUiTests : IDisposable
{
    public void Dispose() => UiText.Language = UiLanguage.English;

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ru")]
    public void HelpUsesTheNamesOfTheUi(string code)
    {
        UiText.Language = UiLanguages.FromCode(code)!.Value;
        var help = string.Concat(HelpDocument.Load(code).Topics
            .SelectMany(topic => topic.Blocks)
            .SelectMany(block => block.Spans)
            .Select(span => span.Text));

        string[] names =
        [
            UiText.GroupInput,
            UiText.LoadList,
            UiText.Download,
            UiText.Cancel,
            UiText.SelectOnlyNew,
            UiText.HideLog,
            UiText.SkipExisting,
            UiText.RandomPause,
            UiText.DownloadSubtitles,
            UiText.StatusNew,
            UiText.StatusExisting,
            UiText.StatusUnavailable,
            UiText.TabSettings,
            UiText.ToolsSetUp,
            UiText.ToolsUpdate,
            UiText.CookiesNone,
            UiText.SubtitlesLabel.TrimEnd(':'),
            UiText.LimitLabel.TrimEnd(':'),
            UiText.CookiesLabel.TrimEnd(':'),
            UiText.ApiKeyLabel.TrimEnd(':'),
            UiText.ToolsLabel.TrimEnd(':'),
            .. Enum.GetValues<VideoQuality>().Select(UiText.QualityName),
        ];
        Assert.All(names, name => Assert.Contains(name, help));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    [InlineData("ru")]
    public void HelpReferencesPointToExistingTopics(string code)
    {
        UiText.Language = UiLanguages.FromCode(code)!.Value;
        var document = HelpDocument.Load(code);

        // Cross references in the problems topic use the titles of other topics.
        var problems = string.Concat(document.Find(HelpTopics.Problems)!.Blocks.SelectMany(b => b.Spans).Select(s => s.Text));
        Assert.Contains(document.Find(HelpTopics.Pause)!.Title, problems);
        Assert.Contains(document.Find(HelpTopics.Cookies)!.Title, problems);
    }

    [Theory]
    [InlineData("ERROR: Could not copy Chrome cookie database.", HelpTopics.Cookies)]
    [InlineData("ERROR: [youtube] x: Sign in to confirm you're not a bot", HelpTopics.Cookies)]
    [InlineData("ERROR: unable to download video data: HTTP Error 429: Too Many Requests", HelpTopics.Pause)]
    [InlineData("ERROR: ffmpeg not found", HelpTopics.Tools)]
    [InlineData("ERROR: something else", null)]
    public void KnownErrorsPointToAHelpTopic(string error, string? topic)
    {
        Assert.Equal(topic, YtDlpOutput.GetHelpTopic(error));
        if (topic is not null)
            Assert.Contains(topic, HelpTopics.All);
    }
}
