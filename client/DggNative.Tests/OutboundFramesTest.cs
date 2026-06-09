using DggNative.Models;

namespace DggNative.Tests;

public class OutboundFramesTest
{
    [Fact]
    public void HelloFrameMatchesServerProtocol()
    {
        Assert.Equal("""HELLO {"nick":"jack_1"}""", OutboundFrames.Hello("jack_1"));
    }

    [Fact]
    public void MsgFrameFlattensUserFieldsToTopLevel()
    {
        var user = new User
        {
            Id = 7,
            Nick = "jack_1",
            Roles = ["USER"],
            Features = [],
            CreatedDate = "2026-06-09T12:00:00.000Z",
        };

        var frame = OutboundFrames.Msg(user, "hello world");

        Assert.StartsWith("MSG {", frame);
        Assert.Contains("""
                        "data":"hello world"
                        """, frame);
        Assert.Contains("""
                        "nick":"jack_1"
                        """, frame);
        Assert.DoesNotContain("\"User\"", frame);
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
