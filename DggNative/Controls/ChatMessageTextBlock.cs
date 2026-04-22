using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using DggNative.Rendering;
using DggNative.Services;

namespace DggNative.Controls;

public class ChatMessageTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> NickTextProperty =
        AvaloniaProperty.Register<ChatMessageTextBlock, string?>(nameof(NickText));

    public static readonly StyledProperty<IBrush?> NickForegroundProperty =
        AvaloniaProperty.Register<ChatMessageTextBlock, IBrush?>(nameof(NickForeground));

    public static readonly StyledProperty<string?> MessageTextProperty =
        AvaloniaProperty.Register<ChatMessageTextBlock, string?>(nameof(MessageText));

    private EmoteCatalogService? _catalogService;
    private int _renderVersion;

    public string? NickText
    {
        get => GetValue(NickTextProperty);
        set => SetValue(NickTextProperty, value);
    }

    public IBrush? NickForeground
    {
        get => GetValue(NickForegroundProperty);
        set => SetValue(NickForegroundProperty, value);
    }

    public string? MessageText
    {
        get => GetValue(MessageTextProperty);
        set => SetValue(MessageTextProperty, value);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AttachCatalogService();
        RebuildInlines();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        DetachCatalogService();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == NickTextProperty
            || change.Property == NickForegroundProperty
            || change.Property == MessageTextProperty
            || change.Property == FontSizeProperty)
        {
            RebuildInlines();
        }
    }

    private void AttachCatalogService()
    {
        var service = (Application.Current as App)?.EmoteCatalogService;
        if (ReferenceEquals(_catalogService, service))
        {
            return;
        }

        DetachCatalogService();
        _catalogService = service;

        if (_catalogService is not null)
        {
            _catalogService.CatalogUpdated += CatalogService_OnCatalogUpdated;
        }
    }

    private void DetachCatalogService()
    {
        if (_catalogService is null)
        {
            return;
        }

        _catalogService.CatalogUpdated -= CatalogService_OnCatalogUpdated;
        _catalogService = null;
    }

    private void CatalogService_OnCatalogUpdated(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(RebuildInlines);
    }

    private void RebuildInlines()
    {
        AttachCatalogService();

        var inlines = Inlines;
        if (inlines is null)
        {
            inlines = new InlineCollection();
            Inlines = inlines;
        }

        inlines.Clear();

        var renderVersion = unchecked(++_renderVersion);
        var nick = NickText ?? string.Empty;

        var nickRun = new Run(nick)
        {
            FontWeight = FontWeight.Medium
        };

        if (NickForeground is not null)
        {
            nickRun.Foreground = NickForeground;
        }

        inlines.Add(nickRun);
        inlines.Add(new Run(":"));

        var message = MessageText;
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        var parts = _catalogService?.ParseMessage(message) ?? [ChatInlinePart.TextPart(message)];
        foreach (var part in parts)
        {
            if (!part.IsEmote)
            {
                inlines.Add(new Run(part.Text));
                continue;
            }

            var host = CreatePlaceholder(part.Text);
            inlines.Add(host);
            _ = PopulateEmoteAsync(host, part, renderVersion);
        }
    }

    private Border CreatePlaceholder(string emoteName)
    {
        return new Border
        {
            Child = new TextBlock
            {
                Text = emoteName,
                FontSize = FontSize
            },
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
    }

    private async Task PopulateEmoteAsync(Border host, ChatInlinePart part, int renderVersion)
    {
        if (_catalogService is null || part.ImageUrl is null)
        {
            return;
        }

        var bitmap = await _catalogService.GetImageAsync(part.ImageUrl).ConfigureAwait(false);
        if (bitmap is null)
        {
            return;
        }

        var height = Math.Max(18, Math.Ceiling(FontSize * 1.35));
        var width = bitmap.Size.Height <= 0
            ? height
            : Math.Max(height, Math.Ceiling(bitmap.Size.Width * (height / bitmap.Size.Height)));

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_renderVersion != renderVersion)
            {
                return;
            }

            host.Child = new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.Uniform,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
        });
    }
}
