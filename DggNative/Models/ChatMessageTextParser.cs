using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DggNative.Models;

public static partial class ChatMessageTextParser
{
    private static readonly char[] TrailingUrlPunctuation = ['.', ',', '!', '?', ';', ':', ')', ']', '}'];

    public static IReadOnlyList<ChatMessageTextSegment> Parse(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var segments = new List<ChatMessageTextSegment>();
        var currentIndex = 0;

        foreach (Match match in UrlRegex().Matches(text))
        {
            var candidate = match.Value;
            var urlText = candidate.TrimEnd(TrailingUrlPunctuation);

            if (urlText.Length == 0 || !TryCreateHttpUri(urlText, out var uri))
            {
                continue;
            }

            if (match.Index > currentIndex)
            {
                segments.Add(new ChatMessageTextSegment(text[currentIndex..match.Index], null));
            }

            segments.Add(new ChatMessageTextSegment(urlText, uri));
            currentIndex = match.Index + urlText.Length;
        }

        if (currentIndex < text.Length)
        {
            segments.Add(new ChatMessageTextSegment(text[currentIndex..], null));
        }

        return segments;
    }

    private static bool TryCreateHttpUri(string text, out Uri? uri)
    {
        var uriText = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                      || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? text
            : $"https://{text}";

        if (Uri.TryCreate(uriText, UriKind.Absolute, out uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        uri = null;
        return false;
    }

    [GeneratedRegex(@"(?<![\w@])(?:(?:https?://|www\.)[^\s<>""]+|(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+(?:com|org|net)(?:[/?#][^\s<>""]*)?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UrlRegex();
}
