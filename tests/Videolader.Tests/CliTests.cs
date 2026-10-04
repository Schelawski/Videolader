using Videolader.Core;

namespace Videolader.Tests;

public class CliArgumentsTests
{
    [Fact]
    public void Links_and_options_are_read()
    {
        var result = CliArguments.Parse(
            ["dQw4w9WgXcQ", "https://www.youtube.com/playlist?list=PLabcdefghijk", "--out", @"D:\Videos", "--quality", "720",
             "--subs=ru,de", "--limit", "50", "--pause", "5", "--cookies", "firefox", "--json", "--list"]);

        Assert.Null(result.Error);
        var o = result.Options!;
        Assert.Equal(["dQw4w9WgXcQ", "https://www.youtube.com/playlist?list=PLabcdefghijk"], o.Inputs);
        Assert.Equal(@"D:\Videos", o.OutputFolder);
        Assert.Equal(VideoQuality.Max720, o.Quality);
        Assert.Equal("ru,de", o.SubtitleLanguages);
        Assert.Equal(50, o.Limit);
        Assert.Equal(5, o.PauseSeconds);
        Assert.Equal("firefox", o.CookiesBrowser);
        Assert.True(o.Json);
        Assert.True(o.ListOnly);
    }

    [Fact]
    public void Download_keyword_and_id_options_are_accepted()
    {
        var result = CliArguments.Parse(["download", "--id", "dQw4w9WgXcQ", "--playlist", "PLabcdefghijk"]);

        Assert.Equal(["dQw4w9WgXcQ", "PLabcdefghijk"], result.Options!.Inputs);
    }

    [Fact]
    public void Video_id_starting_with_dash_is_an_input_not_an_option()
    {
        var result = CliArguments.Parse(["-abcdefghij"]);

        Assert.Equal(["-abcdefghij"], result.Options!.Inputs);
    }

    [Theory]
    [InlineData("--quality", "ultra")]
    [InlineData("--limit", "abc")]
    [InlineData("--limit", "-3")]
    [InlineData("--pause", "x")]
    [InlineData("--bogus", "1")]
    public void Wrong_values_give_an_error(string option, string value)
    {
        var result = CliArguments.Parse(["dQw4w9WgXcQ", option, value]);

        Assert.Null(result.Options);
        Assert.False(string.IsNullOrEmpty(result.Error));
    }

    [Fact]
    public void Missing_value_gives_an_error()
    {
        Assert.NotNull(CliArguments.Parse(["dQw4w9WgXcQ", "--out"]).Error);
    }

    [Fact]
    public void Without_input_there_is_an_error_but_help_and_setup_are_fine()
    {
        Assert.NotNull(CliArguments.Parse(["--json"]).Error);
        Assert.True(CliArguments.Parse(["--help"]).Options!.Help);
        Assert.True(CliArguments.Parse(["setup"]).Options!.Setup);
    }

    [Theory]
    [InlineData("UCabcdefghijklmnopqrstuv", "https://www.youtube.com/channel/UCabcdefghijklmnopqrstuv/videos")]
    [InlineData("@UrokiMeditation", "https://www.youtube.com/@UrokiMeditation/videos")]
    public void Channel_id_and_handle_without_link_are_recognized(string input, string expectedUrl)
    {
        var item = InputParser.ParseToken(input);

        Assert.NotNull(item);
        Assert.Equal(InputKind.Url, item.Kind);
        Assert.Equal(expectedUrl, item.Url);
    }
}
