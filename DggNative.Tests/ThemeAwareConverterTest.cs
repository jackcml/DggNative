using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using DggNative.Converters;
using DggNative.Models;

namespace DggNative.Tests;

public class ThemeAwareConverterTest
{
    private readonly SolidColorBrush _mentionBrush = SolidColorBrush.Parse("#D9ECFF");
    private readonly SolidColorBrush _ownMessageBrush = SolidColorBrush.Parse("#EEF1F4");
    private readonly SolidColorBrush _defaultNameBrush = SolidColorBrush.Parse("#1F2328");
    private readonly SolidColorBrush _tier1Brush = SolidColorBrush.Parse("#0969DA");

    public ThemeAwareConverterTest()
    {
        EnsureApplicationResources();
    }

    [Fact]
    public void MessageBackgroundConverterUsesThemedOwnMessageBrush()
    {
        var converter = new MessageBackgroundConverter();

        var result = converter.Convert(
            ["hello", "localUser", "LOCALUSER", ThemeVariant.Light],
            typeof(IBrush),
            null,
            CultureInfo.InvariantCulture);

        Assert.Same(_ownMessageBrush, result);
    }

    [Fact]
    public void MessageBackgroundConverterUsesThemedMentionBrush()
    {
        var converter = new MessageBackgroundConverter();

        var result = converter.Convert(
            ["hello localUser", "localUser", "otherUser", ThemeVariant.Light],
            typeof(IBrush),
            null,
            CultureInfo.InvariantCulture);

        Assert.Same(_mentionBrush, result);
    }

    [Fact]
    public void MessageBackgroundConverterKeepsUnhighlightedMessagesTransparent()
    {
        var converter = new MessageBackgroundConverter();

        var result = converter.Convert(
            ["hello", "localUser", "otherUser", ThemeVariant.Light],
            typeof(IBrush),
            null,
            CultureInfo.InvariantCulture);

        Assert.Same(Brushes.Transparent, result);
    }

    [Fact]
    public void SubscriptionToColorConverterUsesThemedTierBrush()
    {
        var converter = new SubscriptionToColorConverter();

        var result = converter.Convert(
            [new Subscription { Tier = 1, Source = "destiny.gg" }, ThemeVariant.Light],
            typeof(IBrush),
            null,
            CultureInfo.InvariantCulture);

        Assert.Same(_tier1Brush, result);
    }

    [Fact]
    public void SubscriptionToColorConverterFallsBackToThemedDefaultNameBrush()
    {
        var converter = new SubscriptionToColorConverter();

        var result = converter.Convert(
            [null, ThemeVariant.Light],
            typeof(IBrush),
            null,
            CultureInfo.InvariantCulture);

        Assert.Same(_defaultNameBrush, result);
    }

    private void EnsureApplicationResources()
    {
        if (Application.Current is null)
        {
            AppBuilder.Configure<Application>()
                .UsePlatformDetect()
                .SetupWithoutStarting();
        }

        var application = Application.Current!;
        application.Resources = new ResourceDictionary
        {
            ThemeDictionaries =
            {
                [ThemeVariant.Light] = new ResourceDictionary
                {
                    ["ChatMentionBackgroundBrush"] = _mentionBrush,
                    ["ChatOwnMessageBackgroundBrush"] = _ownMessageBrush,
                    ["DefaultChatNameBrush"] = _defaultNameBrush,
                    ["Tier1Brush"] = _tier1Brush,
                },
            },
        };
    }
}
