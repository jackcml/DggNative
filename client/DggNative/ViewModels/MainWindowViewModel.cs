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
    private const int MaxBufferedMessages = 500;

    private readonly WebSocketChatService _chatService;
    private readonly AuthenticationService _authService;
    private readonly CookiePersistenceService _cookiePersistence;
    private readonly SettingsPersistenceService _settingsPersistence;
    private readonly IDesktopNotificationService _desktopNotifications;

    private AppSettings _settings = new();
    private ChatServerConfig _config = new(ChatServerConfig.OfficialUri, ChatAuthMode.Cookie);

    [ObservableProperty] private bool _isConnected;

    [ObservableProperty] private string _connectionStatusText = "Disconnected";

    [ObservableProperty] private User? _localUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoginButton))]
    [NotifyPropertyChangedFor(nameof(ShowJoinBar))]
    private bool _isLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoginButton))]
    [NotifyPropertyChangedFor(nameof(ShowJoinBar))]
    [NotifyPropertyChangedFor(nameof(LogoutButtonText))]
    private bool _isCustomServer;

    [ObservableProperty] private string _nickInput = string.Empty;

    [ObservableProperty] private string? _joinError;

    [ObservableProperty] private bool _isWindowFocused = true;

    public bool ShowLoginButton => !IsLoggedIn && !IsCustomServer;
    public bool ShowJoinBar => !IsLoggedIn && IsCustomServer;
    public string LogoutButtonText => IsCustomServer ? "Leave" : "Logout";

    public AvaloniaList<ChatMessage> MessageList { get; } = [];

    public event EventHandler? MentionNotificationActivated;

    public MainWindowViewModel(
        WebSocketChatService chatService,
        AuthenticationService authService,
        CookiePersistenceService cookiePersistence,
        SettingsPersistenceService settingsPersistence,
        IDesktopNotificationService desktopNotifications)
    {
        _chatService = chatService;
        _authService = authService;
        _cookiePersistence = cookiePersistence;
        _settingsPersistence = settingsPersistence;
        _desktopNotifications = desktopNotifications;
        _desktopNotifications.NotificationActivated += (_, _) =>
            MentionNotificationActivated?.Invoke(this, EventArgs.Empty);

        chatService.MessageStream.OfType<ChatMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                MessageList.Add(item);
                TrimBufferedMessages();

                if (!IsWindowFocused && ChatMentionMatcher.MentionsUser(item, LocalUser))
                {
                    _desktopNotifications.ShowMentionNotification(item);
                }
            });

        chatService.MessageStream.OfType<HistoryMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                MessageList.Clear();
                MessageList.AddRange(item.Messages.OfType<ChatMessage>().TakeLast(MaxBufferedMessages));
                // FIXME: handle other message types if necessary
            });

        chatService.MessageStream.OfType<MeMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                LocalUser = item.User;
                IsLoggedIn = true;
                JoinError = null;
            });

        chatService.IsConnected.ObserveOn(new AvaloniaSynchronizationContext()).Subscribe(status =>
        {
            IsConnected = status is ConnectionStatusConnected;
            ConnectionStatusText = status switch
            {
                ConnectionStatusConnected => "Connected",
                ConnectionStatusDisconnected => "Disconnected",
                ConnectionStatusConnecting => "Connecting...",
                ConnectionStatusRetrying r => $"Retrying in {Math.Ceiling(r.MillisecondsUntilRetry / 1000.0)}s...",
                ConnectionStatusRejected r => $"Rejected: {r.Reason}",
                _ => "Unknown"
            };

            if (status is ConnectionStatusRejected rejected && IsCustomServer)
            {
                // Latch the reason: the reconnect loop immediately moves on to Retrying,
                // so ConnectionStatusText alone would flash past the user.
                JoinError = rejected.Reason;
                IsLoggedIn = false;
                LocalUser = null;
            }
        });

        // Resolve the server config and persisted credentials, then connect
        Task.Run(async () =>
        {
            _settings = await _settingsPersistence.LoadAsync() ?? new AppSettings();
            _config = ChatServerConfig.Resolve(Environment.GetEnvironmentVariable("wsurl"), _settings);

            Dispatcher.UIThread.Post(() =>
            {
                IsCustomServer = _config.AuthMode == ChatAuthMode.Hello;
                NickInput = _settings.Nick ?? string.Empty;
            });

            if (_config.AuthMode == ChatAuthMode.Cookie)
            {
                var saved = await _cookiePersistence.LoadAsync();
                if (saved is { HasCredentials: true })
                {
                    _chatService.SetAuthCookies(saved);
                    Dispatcher.UIThread.Post(() => IsLoggedIn = true);
                }
            }

            _chatService.Configure(_config);
            await chatService.StartAsync();
        });
    }

    // Parameterless constructor for the XAML designer only
    public MainWindowViewModel()
    {
        _chatService = null!;
        _authService = null!;
        _cookiePersistence = null!;
        _settingsPersistence = null!;
        _desktopNotifications = null!;
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
        if (IsCustomServer)
        {
            // Leave: drop the claimed nick (the persisted nick stays for pre-fill)
            _config = _config with { HelloNick = null };
            _chatService.Configure(_config);
        }
        else
        {
            _chatService.SetAuthCookies(null);
            _cookiePersistence.Clear();
        }

        IsLoggedIn = false;
        LocalUser = null;

        // Reconnect anonymously
        await Task.Run(_chatService.ReconnectAsync);
    }

    [RelayCommand]
    private async Task JoinAsync()
    {
        var nick = NickInput.Trim();
        if (!OutboundFrames.IsValidNick(nick))
        {
            JoinError = "Nick must be 1-32 chars and use letters, numbers, underscores, or hyphens.";
            return;
        }

        JoinError = null;
        NickInput = nick;
        _settings = _settings with { Nick = nick };
        await _settingsPersistence.SaveAsync(_settings);

        _config = _config with { HelloNick = nick };
        _chatService.Configure(_config);
        await Task.Run(_chatService.ReconnectAsync);
    }

    [RelayCommand]
    private async Task OpenSettingsAsync(Window owner)
    {
        var dialog = new Views.SettingsWindow
        {
            DataContext = new SettingsWindowViewModel(_settings, Environment.GetEnvironmentVariable("wsurl")),
        };

        var result = await dialog.ShowDialog<AppSettings?>(owner);
        if (result == null) return;

        _settings = result;
        await _settingsPersistence.SaveAsync(_settings);

        var newConfig = ChatServerConfig.Resolve(Environment.GetEnvironmentVariable("wsurl"), _settings);
        if (newConfig.ServerUri == _config.ServerUri && newConfig.AuthMode == _config.AuthMode) return;

        _config = newConfig;
        MessageList.Clear();
        IsLoggedIn = false;
        LocalUser = null;
        JoinError = null;
        IsCustomServer = _config.AuthMode == ChatAuthMode.Hello;
        NickInput = _settings.Nick ?? NickInput;

        if (_config.AuthMode == ChatAuthMode.Cookie)
        {
            var saved = await _cookiePersistence.LoadAsync();
            if (saved is { HasCredentials: true })
            {
                _chatService.SetAuthCookies(saved);
                IsLoggedIn = true;
            }
        }

        _chatService.Configure(_config);
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

    private void TrimBufferedMessages()
    {
        while (MessageList.Count > MaxBufferedMessages)
        {
            MessageList.RemoveAt(0);
        }
    }
}
