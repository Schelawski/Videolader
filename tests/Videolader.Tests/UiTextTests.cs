using System.Globalization;
using System.Reflection;
using Videolader.Core;

namespace Videolader.Tests;

/// <summary>
/// <see cref="UiText.Language"/> is global, so all tests that read translated texts run in this collection, one at a time.
/// </summary>
[CollectionDefinition(nameof(UiTextCollection), DisableParallelization = true)]
public sealed class UiTextCollection;

/// <summary>Each test starts in English, the default language; tests switch where needed.</summary>
[Collection(nameof(UiTextCollection))]
public sealed class UiTextTests : IDisposable
{
    // Texts without Russian letters on purpose (key names, the sign "№").
    private static readonly HashSet<string> WithoutCyrillic = [nameof(UiText.CtrlEnterHint), nameof(UiText.ColumnNumber)];

    // Texts that are the same in English and German on purpose (international words).
    private static readonly HashSet<string> SameInEnglishAndGerman =
    [
        nameof(UiText.TabDownload),
        nameof(UiText.PauseLabel),
        nameof(UiText.SecondsUnit),
        nameof(UiText.LimitLabel),
        nameof(UiText.ColumnPlaylist),
        nameof(UiText.ColumnStatus),
    ];

    public UiTextTests() => UiText.Language = UiLanguage.English;

    public void Dispose() => UiText.Language = UiLanguage.English;

    public static TheoryData<string> TextProperties() =>
        [.. typeof(UiText)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(string))
            .Select(property => property.Name)];

    [Theory]
    [MemberData(nameof(TextProperties))]
    public void EveryTextExistsInAllLanguages(string propertyName)
    {
        var property = typeof(UiText).GetProperty(propertyName)!;

        UiText.Language = UiLanguage.English;
        var english = (string)property.GetValue(null)!;
        UiText.Language = UiLanguage.German;
        var german = (string)property.GetValue(null)!;
        UiText.Language = UiLanguage.Russian;
        var russian = (string)property.GetValue(null)!;

        Assert.False(string.IsNullOrWhiteSpace(english));
        Assert.False(string.IsNullOrWhiteSpace(german));
        Assert.False(string.IsNullOrWhiteSpace(russian));

        // Catches German or Russian text pasted into the English slot.
        Assert.False(ContainsCyrillic(english), $"English text of {propertyName} contains Cyrillic: {english}");
        Assert.False(english.Any(c => "äöüÄÖÜß„".Contains(c)), $"English text of {propertyName} looks German: {english}");
        Assert.False(ContainsCyrillic(german), $"German text of {propertyName} contains Cyrillic: {german}");
        if (!WithoutCyrillic.Contains(propertyName))
            Assert.True(ContainsCyrillic(russian), $"Russian text of {propertyName} is not Russian: {russian}");
        if (!SameInEnglishAndGerman.Contains(propertyName))
            Assert.NotEqual(english, german);
    }

    [Fact]
    public void ThereAreTextsToCheck()
    {
        Assert.True(TextProperties().Count > 100);
    }

    [Theory]
    [InlineData(UiLanguage.English)]
    [InlineData(UiLanguage.German)]
    [InlineData(UiLanguage.Russian)]
    public void CommandLineHelpNamesEveryOption(UiLanguage language)
    {
        UiText.Language = language;

        foreach (var option in new[]
                 {
                     "setup", "--help", "--id", "--playlist", "--url", "--out", "--quality", "--subs", "--no-subs", "--limit",
                     "--pause", "--cookies", "--no-skip", "--list", "--json", "--verbose", "--lang",
                 })
        {
            Assert.Contains(option, CliArguments.HelpText);
        }
    }

    [Theory]
    [InlineData(UiLanguage.English, 1, "1 video")]
    [InlineData(UiLanguage.English, 5, "5 videos")]
    [InlineData(UiLanguage.German, 1, "1 Video")]
    [InlineData(UiLanguage.German, 5, "5 Videos")]
    [InlineData(UiLanguage.Russian, 1, "1 видео")]
    [InlineData(UiLanguage.Russian, 21, "21 видео")]
    public void VideoCountUsesSingularAndPlural(UiLanguage language, int count, string expected)
    {
        UiText.Language = language;

        Assert.Equal(expected, UiText.Videos(count));
    }

    [Fact]
    public void QualityNamesAreTranslated()
    {
        UiText.Language = UiLanguage.German;
        Assert.Equal("Nur Audio (M4A)", VideoQuality.AudioOnly.GetDisplayName());
        UiText.Language = UiLanguage.Russian;
        Assert.Equal("Только аудио (M4A)", VideoQuality.AudioOnly.GetDisplayName());
        UiText.Language = UiLanguage.English;
        Assert.Equal("Audio only (M4A)", VideoQuality.AudioOnly.GetDisplayName());
    }

    [Fact]
    public void SubtitlesSuffixIsEmptyWithoutSubtitles()
    {
        Assert.Equal("Done", UiText.StatusDone([]));
        Assert.Equal("Done + subtitles ru, de", UiText.StatusDone(["ru", "de"]));
    }

    private static bool ContainsCyrillic(string text) => text.Any(c => c is >= 'Ѐ' and <= 'ӿ');
}

public class UiLanguageTests
{
    [Theory]
    [InlineData("de-DE", UiLanguage.German)]
    [InlineData("de-AT", UiLanguage.German)]
    [InlineData("ru-RU", UiLanguage.Russian)]
    [InlineData("en-US", UiLanguage.English)]
    [InlineData("fr-FR", UiLanguage.English)]
    [InlineData("uk-UA", UiLanguage.English)]
    public void WithoutAChoiceTheWindowsLanguageDecidesAndEnglishIsTheDefault(string culture, UiLanguage expected)
    {
        Assert.Equal(expected, UiLanguages.Resolve(string.Empty, CultureInfo.GetCultureInfo(culture)));
    }

    [Theory]
    [InlineData("en", "ru-RU", UiLanguage.English)]
    [InlineData("ru", "de-DE", UiLanguage.Russian)]
    [InlineData(" DE ", "en-US", UiLanguage.German)]
    public void AStoredChoiceWins(string stored, string culture, UiLanguage expected)
    {
        Assert.Equal(expected, UiLanguages.Resolve(stored, CultureInfo.GetCultureInfo(culture)));
    }

    [Fact]
    public void CodesRoundTrip()
    {
        foreach (var language in UiLanguages.All)
            Assert.Equal(language, UiLanguages.FromCode(UiLanguages.Code(language)));
        Assert.Null(UiLanguages.FromCode("fr"));
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData(" EN ", "en")]
    [InlineData("fr", "")]
    [InlineData("", "")]
    public void SettingsKeepOnlyKnownLanguageCodes(string stored, string expected)
    {
        var settings = new AppSettings { UiLanguage = stored };
        settings.Normalize();
        Assert.Equal(expected, settings.UiLanguage);
    }

    [Fact]
    public void CommandLineReadsTheLanguage()
    {
        Assert.Equal(UiLanguage.Russian, CliArguments.Parse(["aqz-KE-bpKQ", "--lang", "ru"]).Options!.Language);
        Assert.Equal(UiLanguage.German, CliArguments.Parse(["aqz-KE-bpKQ", "--lang=de"]).Options!.Language);
        Assert.Null(CliArguments.Parse(["aqz-KE-bpKQ"]).Options!.Language);
        Assert.NotNull(CliArguments.Parse(["aqz-KE-bpKQ", "--lang", "fr"]).Error);
    }
}
