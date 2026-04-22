using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace DggNative.Converters;

public class MentionToBackgroundConverter : IMultiValueConverter
{
    private static readonly IBrush MentionBrush = SolidColorBrush.Parse("#06263e");

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2 || values[0] is not string data || values[1] is not string nick)
            return Brushes.Transparent;
        if (!string.IsNullOrEmpty(nick) && data.Contains(nick, StringComparison.OrdinalIgnoreCase))
        {
            return MentionBrush;
        }

        return Brushes.Transparent;
    }
}
