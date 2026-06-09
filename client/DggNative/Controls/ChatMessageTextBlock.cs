using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using DggNative.Models;

namespace DggNative.Controls;

public class ChatMessageTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> UserNickProperty =
        AvaloniaProperty.Register<ChatMessageTextBlock, string?>(nameof(UserNick));

    public static readonly StyledProperty<IBrush?> UserForegroundProperty =
        AvaloniaProperty.Register<ChatMessageTextBlock, IBrush?>(nameof(UserForeground));

    public static readonly StyledProperty<string?> MessageTextProperty =
        AvaloniaProperty.Register<ChatMessageTextBlock, string?>(nameof(MessageText));

    public string? UserNick
    {
        get => GetValue(UserNickProperty);
        set => SetValue(UserNickProperty, value);
    }

    public IBrush? UserForeground
    {
        get => GetValue(UserForegroundProperty);
        set => SetValue(UserForegroundProperty, value);
    }

    public string? MessageText
    {
        get => GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == UserNickProperty
            || change.Property == UserForegroundProperty
            || change.Property == MessageTextProperty)
        {
            RebuildInlines();
        }
    }

    private void RebuildInlines()
    {
        Inlines ??= new InlineCollection();
        Inlines.Clear();

        Inlines.Add(new Run(UserNick ?? string.Empty)
        {
            FontWeight = FontWeight.SemiBold,
            Foreground = UserForeground,
        });
        Inlines.Add(new Run(": "));

        foreach (var segment in ChatMessageTextParser.Parse(MessageText))
        {
            if (!segment.IsLink)
            {
                Inlines.Add(new Run(segment.Text));
                continue;
            }

            Inlines.Add(new InlineUIContainer(new HyperlinkButton
            {
                Content = segment.Text,
                NavigateUri = segment.Uri,
                FontFamily = FontFamily,
                FontSize = FontSize,
                FontStyle = FontStyle,
                FontWeight = FontWeight,
                Padding = new Thickness(0),
                Margin = new Thickness(0),
                MinHeight = 0,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
            })
            {
                BaselineAlignment = BaselineAlignment.TextBottom,
            });
        }
    }
}
