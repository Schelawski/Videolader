using Videolader.Core;

namespace Videolader.Tests;

public class InputParserTests
{
    [Theory]
    [InlineData("dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("-abcdefghij", "-abcdefghij")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?feature=share&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc", "dQw4w9WgXcQ")]
    [InlineData("youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=RDdQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=WL", "dQw4w9WgXcQ")]
    public void Recognizes_videos(string input, string expectedId)
    {
        var item = InputParser.ParseToken(input);

        Assert.NotNull(item);
        Assert.Equal(InputKind.Video, item.Kind);
        Assert.Equal(expectedId, item.Value);
    }

    [Theory]
    [InlineData("PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG")]
    [InlineData("https://www.youtube.com/playlist?list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG&index=4")]
    [InlineData("https://music.youtube.com/playlist?list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG")]
    public void Recognizes_playlists(string input)
    {
        var item = InputParser.ParseToken(input);

        Assert.NotNull(item);
        Assert.Equal(InputKind.Playlist, item.Kind);
        Assert.Equal("PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG", item.Value);
        Assert.Equal("https://www.youtube.com/playlist?list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG", item.Url);
    }

    [Theory]
    [InlineData("https://www.youtube.com/@AnandaVidya", "https://www.youtube.com/@AnandaVidya/videos")]
    [InlineData("https://www.youtube.com/@AnandaVidya/streams", "https://www.youtube.com/@AnandaVidya/streams")]
    [InlineData("https://www.youtube.com/channel/UC1234567890abcdefghijkl", "https://www.youtube.com/channel/UC1234567890abcdefghijkl/videos")]
    public void Channel_links_list_the_videos_tab(string input, string expectedUrl)
    {
        var item = InputParser.ParseToken(input);

        Assert.NotNull(item);
        Assert.Equal(InputKind.Url, item.Kind);
        Assert.Equal(expectedUrl, item.Value);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("https://vimeo.com/123456")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("dQw4w9WgXc")]
    [InlineData("https://evil-youtube.com/watch?v=dQw4w9WgXcQ")]
    public void Rejects_unknown_input(string input) => Assert.Null(InputParser.ParseToken(input));

    [Fact]
    public void Parses_several_lines_and_removes_duplicates()
    {
        var text = """
            https://youtu.be/dQw4w9WgXcQ
              dQw4w9WgXcQ
            PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG, jNQXAC9IVRw
            nonsense
            """;

        var parsed = InputParser.Parse(text);

        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal("dQw4w9WgXcQ", parsed.Items[0].Value);
        Assert.Equal(InputKind.Playlist, parsed.Items[1].Kind);
        Assert.Equal("jNQXAC9IVRw", parsed.Items[2].Value);
        Assert.Equal("nonsense", Assert.Single(parsed.Invalid));
    }
}
