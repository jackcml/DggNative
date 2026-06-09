using DggNative.Models;

namespace DggNative.Tests;

public class ChatMessageTextParserTest
{
    [Fact]
    public void ParseSplitsHttpUrlsFromMessageText()
    {
        var segments = ChatMessageTextParser.Parse("look https://example.com/path ok");

        Assert.Equal(3, segments.Count);
        Assert.Equal("look ", segments[0].Text);
        Assert.False(segments[0].IsLink);
        Assert.Equal("https://example.com/path", segments[1].Text);
        Assert.Equal(new Uri("https://example.com/path"), segments[1].Uri);
        Assert.Equal(" ok", segments[2].Text);
    }

    [Fact]
    public void ParseTreatsWwwUrlsAsHttpsLinks()
    {
        var segments = ChatMessageTextParser.Parse("go www.destiny.gg/bigscreen");

        Assert.Equal(2, segments.Count);
        Assert.Equal("www.destiny.gg/bigscreen", segments[1].Text);
        Assert.Equal(new Uri("https://www.destiny.gg/bigscreen"), segments[1].Uri);
    }

    [Fact]
    public void ParseTreatsBareCommonDomainsAsHttpsLinks()
    {
        var segments = ChatMessageTextParser.Parse("try example.com or example.org/path?q=1#top");

        Assert.Equal(4, segments.Count);
        Assert.Equal("example.com", segments[1].Text);
        Assert.Equal(new Uri("https://example.com"), segments[1].Uri);
        Assert.Equal("example.org/path?q=1#top", segments[3].Text);
        Assert.Equal(new Uri("https://example.org/path?q=1#top"), segments[3].Uri);
    }

    [Fact]
    public void ParseDoesNotTreatEmailDomainsAsLinks()
    {
        var segments = ChatMessageTextParser.Parse("send mail to test@example.net");

        Assert.Single(segments);
        Assert.False(segments[0].IsLink);
    }

    [Fact]
    public void ParseKeepsTrailingPunctuationOutsideTheLink()
    {
        var segments = ChatMessageTextParser.Parse("watch https://example.com/test).");

        Assert.Equal(3, segments.Count);
        Assert.Equal("https://example.com/test", segments[1].Text);
        Assert.Equal(").", segments[2].Text);
    }

    [Fact]
    public void ParseSupportsMultipleUrls()
    {
        var segments = ChatMessageTextParser.Parse("a https://one.test b http://two.test");

        Assert.Equal(4, segments.Count);
        Assert.Equal(new Uri("https://one.test"), segments[1].Uri);
        Assert.Equal(new Uri("http://two.test"), segments[3].Uri);
    }
}
