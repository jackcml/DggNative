using DggNative.Models;

namespace DggNative.Tests;

public class OutboundFramesTest
{
    [Fact]
    public void HelloFrameMatchesServerProtocol()
    {
        Assert.Equal("""HELLO {"nick":"jack_1"}""", OutboundFrames.Hello("jack_1"));
    }

    [Theory]
    [InlineData("jack_1", true)]
    [InlineData("a", true)]
    [InlineData("user-name", true)]
    [InlineData("-leading-hyphen", false)]
    [InlineData("bad nick", false)]
    [InlineData("", false)]
    public void ValidatesNicksLikeTheServer(string nick, bool expected)
    {
        Assert.Equal(expected, OutboundFrames.IsValidNick(nick));
    }

    [Fact]
    public void RejectsNicksOverThirtyTwoChars()
    {
        Assert.True(OutboundFrames.IsValidNick(new string('a', 32)));
        Assert.False(OutboundFrames.IsValidNick(new string('a', 33)));
    }
}
