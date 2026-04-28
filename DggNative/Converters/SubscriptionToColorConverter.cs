using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using DggNative.Models;

namespace DggNative.Converters;

public class SubscriptionToColorConverter : IValueConverter, IMultiValueConverter
{
    private static readonly IBrush FallbackDefaultNameBrush = SolidColorBrush.Parse("#1F2328");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Convert(value, parameter as ThemeVariant);
    }

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var subscription = values.Count > 0 ? values[0] : null;
        var themeVariant = values.Count > 1 ? values[1] as ThemeVariant : null;

        return Convert(subscription, themeVariant);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static IBrush Convert(object? value, ThemeVariant? themeVariant)
    {
        if (value is Subscription sub &&
            FindBrush($"Tier{sub.Tier}Brush", themeVariant) is { } tierBrush)
        {
            return tierBrush;
        }

        return FindBrush("DefaultChatNameBrush", themeVariant) ?? FallbackDefaultNameBrush;
    }

    private static IBrush? FindBrush(string key, ThemeVariant? themeVariant)
    {
        if (Application.Current?.TryFindResource(key, themeVariant, out var resource) == true &&
            resource is IBrush brush)
        {
            return brush;
        }

        return null;
    }
}
