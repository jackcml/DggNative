using System;

namespace DggNative.Models;

public static class ChatMentionMatcher
{
    public static bool MentionsUser(string? message, string? nick)
    {
        return !string.IsNullOrEmpty(message)
               && !string.IsNullOrEmpty(nick)
               && message.Contains(nick, StringComparison.OrdinalIgnoreCase);
    }

    public static bool MentionsUser(ChatMessage message, User? localUser)
    {
        return localUser is not null
               && !string.Equals(localUser.Nick, message.User.Nick, StringComparison.OrdinalIgnoreCase)
               && MentionsUser(message.Data, localUser.Nick);
    }
}
