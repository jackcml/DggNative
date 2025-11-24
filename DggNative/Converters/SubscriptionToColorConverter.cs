using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Controls;
using Avalonia.Media;
using DggNative.Models;

namespace DggNative.Converters;

public class SubscriptionToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        object? brush = null;
        if (value is Subscription sub)
            Application.Current?.TryFindResource($"Tier{sub.Tier}Brush", out brush);
        return brush ?? Brushes.White;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}