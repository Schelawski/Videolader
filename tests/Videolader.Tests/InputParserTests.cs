using Videolader.Core;

namespace Videolader.Tests;

public class InputParserTests
{
    [Theory]
    [InlineData("aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("-abcdefghij", "-abcdefghij")]
    [InlineData("https://www.youtube.com/watch?v=aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube.com/watch?v=aqz-KE-bpKQ&t=42s", "aqz-KE-bpKQ")]
    [InlineData("https://m.youtube.com/watch?feature=share&v=aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://youtu.be/aqz-KE-bpKQ?si=abc", "aqz-KE-bpKQ")]
    [InlineData("youtu.be/aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube.com/shorts/aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube.com/live/aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube.com/embed/aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("www.youtube.com/watch?v=aqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube.com/watch?v=aqz-KE-bpKQ&list=RDaqz-KE-bpKQ", "aqz-KE-bpKQ")]
    [InlineData("https://www.youtube.com/watch?v=aqz-KE-bpKQ&list=WL", "aqz-KE-bpKQ")]
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
    [InlineData("https://www.youtube.com/watch?v=aqz-KE-bpKQ&list=PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG&index=4")]
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
    [InlineData("https://evil-youtube.com/watch?v=aqz-KE-bpKQ")]
    public void Rejects_unknown_input(string input) => Assert.Null(InputParser.ParseToken(input));

    [Fact]
    public void Parses_several_lines_and_removes_duplicates()
    {
        var text = """
            https://youtu.be/aqz-KE-bpKQ
              aqz-KE-bpKQ
            PLx0sYbCqOb8TBPRdmBHs5Iftvv9TPboYG, jNQXAC9IVRw
            nonsense
            """;

        var parsed = InputParser.Parse(text);

        Assert.Equal(3, parsed.Items.Count);
        Assert.Equal("aqz-KE-bpKQ", parsed.Items[0].Value);
        Assert.Equal(InputKind.Playlist, parsed.Items[1].Kind);
        Assert.Equal("jNQXAC9IVRw", parsed.Items[2].Value);
        Assert.Equal("nonsense", Assert.Single(parsed.Invalid));
    }
}
