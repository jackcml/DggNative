using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DggNative.Converters;

public class MessageBackgroundConverter : IMultiValueConverter
{
    private static readonly IBrush MentionBrush = SolidColorBrush.Parse("#06263e");
    private static readonly IBrush OwnMessageBrush = SolidColorBrush.Parse("#151515");

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 3 || values[0] is not string data || values[1] is not string localNick ||
            values[2] is not string senderNick || string.IsNullOrEmpty(localNick)) return Brushes.Transparent;

        // Sent by the user
        if (string.Equals(localNick, senderNick, StringComparison.OrdinalIgnoreCase))
        {
            return OwnMessageBrush;
        }

        // User is mentioned
        if (data.Contains(localNick, StringComparison.OrdinalIgnoreCase))
        {
            return MentionBrush;
        }

        return Brushes.Transparent;
    }
}
