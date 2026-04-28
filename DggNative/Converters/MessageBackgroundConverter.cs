using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using DggNative.Models;

namespace DggNative.Converters;

public class MessageBackgroundConverter : IMultiValueConverter
{
    private static readonly IBrush FallbackMentionBrush = SolidColorBrush.Parse("#D9ECFF");
    private static readonly IBrush FallbackOwnMessageBrush = SolidColorBrush.Parse("#EEF1F4");

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3 || values[0] is not string data || values[1] is not string localNick ||
            values[2] is not string senderNick || string.IsNullOrEmpty(localNick)) return Brushes.Transparent;

        var themeVariant = values.Count > 3 ? values[3] as ThemeVariant : null;

        // Sent by the user
        if (string.Equals(localNick, senderNick, StringComparison.OrdinalIgnoreCase))
        {
            return FindBrush("ChatOwnMessageBackgroundBrush", themeVariant, FallbackOwnMessageBrush);
        }

        // User is mentioned
        if (ChatMentionMatcher.MentionsUser(data, localNick))
        {
            return FindBrush("ChatMentionBackgroundBrush", themeVariant, FallbackMentionBrush);
        }

        return Brushes.Transparent;
    }

    private static IBrush FindBrush(string key, ThemeVariant? themeVariant, IBrush fallback)
    {
        if (Application.Current?.TryFindResource(key, themeVariant, out var resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        return fallback;
    }
}
