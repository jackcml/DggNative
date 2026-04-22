using System.Collections.Frozen;
using System.Collections.Generic;

namespace DggNative.Rendering;

public static class EmoteTokenizer
{
    public static IReadOnlyList<ChatInlinePart> Tokenize(
        string? message,
        FrozenDictionary<string, string> emotes)
    {
        if (string.IsNullOrEmpty(message))
        {
            return [];
        }

        List<ChatInlinePart>? parts = null;
        var textStart = 0;
        var index = 0;

        while (index < message.Length)
        {
            while (index < message.Length && char.IsWhiteSpace(message[index]))
            {
                index++;
            }

            if (index >= message.Length)
            {
                break;
            }

            var tokenStart = index;
            while (index < message.Length && !char.IsWhiteSpace(message[index]))
            {
                index++;
            }

            var token = message[tokenStart..index];
            if (!emotes.TryGetValue(token, out var imageUrl))
            {
                continue;
            }

            parts ??= [];

            if (textStart < tokenStart)
            {
                parts.Add(ChatInlinePart.TextPart(message[textStart..tokenStart]));
            }

            parts.Add(ChatInlinePart.EmotePart(token, imageUrl));
            textStart = index;
        }

        if (parts is null)
        {
            return [ChatInlinePart.TextPart(message)];
        }

        if (textStart < message.Length)
        {
            parts.Add(ChatInlinePart.TextPart(message[textStart..]));
        }

        return parts;
    }
}
