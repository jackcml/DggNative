using DggNative.Models;

namespace DggNative.Tests;

public class ChatMentionMatcherTest
{
    [Fact]
    public void MentionsUserMatchesCaseInsensitively()
    {
        Assert.True(ChatMentionMatcher.MentionsUser("hello INFINITT", "Infinitt"));
    }

    [Fact]
    public void MentionsUserIgnoresOwnMessages()
    {
        var localUser = CreateUser("Infinitt");
        var message = new ChatMessage
        {
            User = CreateUser("infinitt"),
            Data = "Infinitt",
        };

        Assert.False(ChatMentionMatcher.MentionsUser(message, localUser));
    }

    [Fact]
    public void MentionsUserRequiresKnownLocalUser()
    {
        var message = new ChatMessage
        {
            User = CreateUser("Other"),
            Data = "hello Infinitt",
        };

        Assert.False(ChatMentionMatcher.MentionsUser(message, null));
    }

    private static User CreateUser(string nick)
    {
        return new User
        {
            Id = 1,
            Nick = nick,
            Roles = [],
            Features = [],
            CreatedDate = "2026-04-23T00:00:00Z",
        };
    }
}
