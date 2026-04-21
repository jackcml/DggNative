using System;
using System.Linq;
using Avalonia.Collections;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DggNative.Models;
using DggNative.Services;

namespace DggNative.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly WebSocketChatService _chatService;
    private readonly AuthenticationService _authService;
    private readonly CookiePersistenceService _cookiePersistence;

    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] private string _connectionStatusText = "Disconnected";

    [ObservableProperty] private User? _localUser;

    [ObservableProperty] private bool _isLoggedIn;

    public AvaloniaList<ChatMessage> MessageList { get; } = [];

    public MainWindowViewModel(
        WebSocketChatService chatService,
        AuthenticationService authService,
        CookiePersistenceService cookiePersistence)
    {
        _chatService = chatService;
        _authService = authService;
        _cookiePersistence = cookiePersistence;

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

        // Try to load persisted cookies, then connect
        Task.Run(async () =>
        {
            var saved = await _cookiePersistence.LoadAsync();
            if (saved is { HasCredentials: true })
            {
                _chatService.SetAuthCookies(saved);
                Dispatcher.UIThread.Post(() => IsLoggedIn = true);
            }

            await chatService.StartAsync();
        });
    }

    // Parameterless constructor for the XAML designer only
    public MainWindowViewModel()
    {
        _chatService = null!;
        _authService = null!;
        _cookiePersistence = null!;
    }

    [RelayCommand]
    private async Task LoginAsync(Window owner)
    {
        var cookies = await _authService.LoginAsync(owner);
        if (cookies == null) return;

        _chatService.SetAuthCookies(cookies);
        IsLoggedIn = true;
        await _cookiePersistence.SaveAsync(cookies);

        // Reconnect with the new credentials
        await Task.Run(_chatService.ReconnectAsync);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        _chatService.SetAuthCookies(null);
        IsLoggedIn = false;
        LocalUser = null;
        _cookiePersistence.Clear();

        // Reconnect anonymously
        await Task.Run(_chatService.ReconnectAsync);
    }

    public async Task SendChatMessageAsync(string message)
    {
        if (!IsConnected || !IsLoggedIn || string.IsNullOrWhiteSpace(message) || message.Length > 512 ||
            LocalUser == null)
            return;

        // Serialize message as JSON
        var json = JsonSerializer.Serialize(new ChatMessage { User = LocalUser, Data = message });
        await _chatService.SendMessageAsync($"MSG {json}", CancellationToken.None);
    }
}