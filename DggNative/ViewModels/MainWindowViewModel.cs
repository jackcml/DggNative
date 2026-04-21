using System;
using System.Linq;
using Avalonia.Collections;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using DggNative.Models;
using DggNative.Services;

namespace DggNative.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IChatService _chatService;

    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] private string _connectionStatusText = "Disconnected";

    [ObservableProperty] private User? _localUser;

    public AvaloniaList<ChatMessage> MessageList { get; } = [];

    public MainWindowViewModel(IChatService chatService)
    {
        _chatService = chatService;

        chatService.MessageStream.OfType<ChatMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item => MessageList.Add(item));

        chatService.MessageStream.OfType<HistoryMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                MessageList.Clear();
                MessageList.AddRange(item.Messages.OfType<ChatMessage>());
                // FIXME: handle other message types if necessary
            });

        chatService.MessageStream.OfType<MeMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item => LocalUser = item.User);

        chatService.IsConnected.ObserveOn(new AvaloniaSynchronizationContext()).Subscribe(status =>
        {
            IsConnected = status is ConnectionStatusConnected;
            ConnectionStatusText = status switch
            {
                ConnectionStatusConnected => "Connected",
                ConnectionStatusDisconnected => "Disconnected",
                ConnectionStatusConnecting => "Connecting...",
                ConnectionStatusRetrying r => $"Retrying in {Math.Ceiling(r.MillisecondsUntilRetry / 1000.0)}s...",
                _ => "Unknown"
            };
        });

        Task.Run(chatService.ConnectAsync);
    }

    public async Task SendChatMessageAsync(string message)
    {
        if (!IsConnected || string.IsNullOrWhiteSpace(message) || message.Length > 512 || LocalUser == null)
            return;

        // Serialize message as JSON
        var json = JsonSerializer.Serialize(new ChatMessage { User = LocalUser, Data = message });
        await _chatService.SendMessageAsync($"MSG {json}", CancellationToken.None);
    }
}