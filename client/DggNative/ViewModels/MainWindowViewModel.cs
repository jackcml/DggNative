using System;
using System.Linq;
using Avalonia.Collections;
using System.Reactive.Linq;
using System.Reactive.Disposables;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DggNative.Models;
using DggNative.Services;

namespace DggNative.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int MaxBufferedMessages = 500;

    private readonly IChatService _chatService;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly AuthenticationService _authService;
    private readonly CredentialPersistenceService _credentialPersistence;
    private readonly SettingsPersistenceService _settingsPersistence;
    private readonly IDesktopNotificationService _desktopNotifications;

    private AppSettings _settings = new();
    private ChatServerConfig _config = new(ChatServerConfig.OfficialUri, ChatAuthMode.Cookie);

    [ObservableProperty] private User? _localUser;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoginButton))]
    [NotifyPropertyChangedFor(nameof(ShowJoinBar))]
    [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
    [NotifyPropertyChangedFor(nameof(ShowConnectionBanner))]
    [NotifyPropertyChangedFor(nameof(ConnectionStatusText))]
    private ChatSessionState _sessionState = new ChatSessionStopped();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoginButton))]
    private bool _credentialsAvailable;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowLoginButton))]
    [NotifyPropertyChangedFor(nameof(ShowJoinBar))]
    [NotifyPropertyChangedFor(nameof(LogoutButtonText))]
    private bool _isCustomServer;

    [ObservableProperty] private string _nickInput = string.Empty;

    [ObservableProperty] private string? _joinError;

    [ObservableProperty] private string? _sendError;

    [ObservableProperty] private string? _securityError;

    [ObservableProperty] private string? _configurationError;

    [ObservableProperty] private bool _isWindowFocused = true;

    public bool IsLoggedIn => SessionState is ChatSessionReady;
    public bool ShowLoginButton => !IsLoggedIn && !IsCustomServer;
    public bool ShowJoinBar => IsCustomServer && SessionState is ChatSessionGuest or ChatSessionRejected;
    public bool ShowConnectionBanner => SessionState is ChatSessionStopped or ChatSessionConnecting
        or ChatSessionRetrying or ChatSessionRejected;
    public string ConnectionStatusText => SessionState switch
    {
        ChatSessionStopped => "Disconnected",
        ChatSessionConnecting => "Connecting...",
        ChatSessionGuest => "Connected as guest",
        ChatSessionAuthenticating => "Identifying...",
        ChatSessionReady => "Connected",
        ChatSessionRetrying retrying =>
            $"Retrying in {Math.Ceiling(retrying.MillisecondsUntilRetry / 1000.0)}s...",
        ChatSessionRejected rejected => $"Rejected: {rejected.Message}",
        _ => "Unknown",
    };
    public string LogoutButtonText => IsCustomServer ? "Leave" : "Logout";

    public AvaloniaList<ChatMessage> MessageList { get; } = [];

    public event EventHandler? MentionNotificationActivated;

    public MainWindowViewModel(
        IChatService chatService,
        AuthenticationService authService,
        CredentialPersistenceService credentialPersistence,
        SettingsPersistenceService settingsPersistence,
        IDesktopNotificationService desktopNotifications)
    {
        _chatService = chatService;
        _authService = authService;
        _credentialPersistence = credentialPersistence;
        _settingsPersistence = settingsPersistence;
        _desktopNotifications = desktopNotifications;
        _desktopNotifications.NotificationActivated += (_, _) =>
            MentionNotificationActivated?.Invoke(this, EventArgs.Empty);

        _subscriptions.Add(chatService.MessageStream.OfType<ChatMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                MessageList.Add(item);
                TrimBufferedMessages();

                if (!IsWindowFocused && ChatMentionMatcher.MentionsUser(item, LocalUser))
                {
                    _desktopNotifications.ShowMentionNotification(item);
                }
            }));

        _subscriptions.Add(chatService.MessageStream.OfType<HistoryMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                MessageList.Clear();
                MessageList.AddRange(item.Messages.OfType<ChatMessage>().TakeLast(MaxBufferedMessages));
                // FIXME: handle other message types if necessary
            }));

        _subscriptions.Add(chatService.MessageStream.OfType<MeMessage>().ObserveOn(new AvaloniaSynchronizationContext())
            .Subscribe(item =>
            {
                LocalUser = item.User;
                JoinError = null;
            }));

        _subscriptions.Add(chatService.MessageStream.OfType<ErrorMessage>()
            .ObserveOn(new AvaloniaSynchronizationContext()).Subscribe(error =>
        {
            if (error.Code == NativeErrorCodes.NickInUse)
            {
                JoinError = error.Message;
            }
            else
            {
                SendError = error.Message;
            }
        }));

        _subscriptions.Add(chatService.SessionState.ObserveOn(new AvaloniaSynchronizationContext()).Subscribe(state =>
        {
            SessionState = state;
            if (state is not ChatSessionReady) LocalUser = null;
            if (state is ChatSessionRejected rejected) JoinError = rejected.Message;
        }));

        // Resolve the server config and persisted credentials, then connect
        Task.Run(async () =>
        {
            try
            {
                _settings = await _settingsPersistence.LoadAsync() ?? new AppSettings();
                _config = ChatServerConfig.Resolve(Environment.GetEnvironmentVariable("wsurl"), _settings);

                Dispatcher.UIThread.Post(() =>
                {
                    IsCustomServer = _config.AuthMode == ChatAuthMode.Hello;
                    NickInput = _settings.Nick ?? string.Empty;
                    ConfigurationError = null;
                });

                if (_config.AuthMode == ChatAuthMode.Cookie)
                {
                    var load = await _credentialPersistence.LoadAsync();
                    Dispatcher.UIThread.Post(() => SecurityError = load.Error);
                    if (load.Credentials is { HasCredentials: true } saved)
                    {
                        _chatService.SetAuthCookies(saved);
                        Dispatcher.UIThread.Post(() => CredentialsAvailable = true);
                    }
                }

                await _chatService.ConfigureAsync(_config);
                await chatService.StartAsync();
            }
            catch (ChatServerConfigurationException ex)
            {
                Dispatcher.UIThread.Post(() => ConfigurationError = ex.Message);
            }
        });
    }

    // Parameterless constructor for the XAML designer only
    public MainWindowViewModel()
    {
        _chatService = null!;
        _authService = null!;
        _credentialPersistence = null!;
        _settingsPersistence = null!;
        _desktopNotifications = null!;
    }

    [RelayCommand]
    private async Task LoginAsync(Window owner)
    {
        var cookies = await _authService.LoginAsync(owner);
        if (cookies == null) return;

        _chatService.SetAuthCookies(cookies);
        CredentialsAvailable = true;
        var save = await _credentialPersistence.SaveAsync(cookies);
        SecurityError = save.Error;

        // Reconnect with the new credentials
        await _chatService.ReconnectAsync();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (IsCustomServer)
        {
            // Leave: drop the claimed nick (the persisted nick stays for pre-fill)
            _config = _config with { HelloNick = null };
            await _chatService.ConfigureAsync(_config);
        }
        else
        {
            _chatService.SetAuthCookies(null);
            var cleared = await _credentialPersistence.ClearAsync();
            SecurityError = cleared.Error;
            try
            {
                await _authService.ClearOfficialSessionAsync();
            }
            catch (Exception ex)
            {
                SecurityError = ex.Message;
            }
        }

        LocalUser = null;
        if (!IsCustomServer) CredentialsAvailable = false;

        // Reconnect anonymously
        await _chatService.ReconnectAsync();
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
        await _chatService.ConfigureAsync(_config);
        await _chatService.ReconnectAsync();
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

        ChatServerConfig newConfig;
        try
        {
            newConfig = ChatServerConfig.Resolve(Environment.GetEnvironmentVariable("wsurl"), _settings);
            ConfigurationError = null;
        }
        catch (ChatServerConfigurationException ex)
        {
            ConfigurationError = ex.Message;
            return;
        }
        if (newConfig.ServerUri == _config.ServerUri && newConfig.AuthMode == _config.AuthMode) return;

        _config = newConfig;
        MessageList.Clear();
        LocalUser = null;
        JoinError = null;
        IsCustomServer = _config.AuthMode == ChatAuthMode.Hello;
        NickInput = _settings.Nick ?? NickInput;

        if (_config.AuthMode == ChatAuthMode.Cookie)
        {
            var load = await _credentialPersistence.LoadAsync();
            SecurityError = load.Error;
            if (load.Credentials is { HasCredentials: true } saved)
            {
                _chatService.SetAuthCookies(saved);
                CredentialsAvailable = true;
            }
        }

        await _chatService.ConfigureAsync(_config);
        await _chatService.ReconnectAsync();
    }

    public async Task<bool> SendChatMessageAsync(string message)
    {
        if (!IsLoggedIn || string.IsNullOrWhiteSpace(message) || message.Length > 512 || LocalUser == null)
            return false;

        var result = await _chatService.SendMessageAsync(
            OutboundFrames.Msg(LocalUser, message), CancellationToken.None);
        SendError = result switch
        {
            SendResult.AcceptedForSend => null,
            SendResult.NotReady => "Chat is not ready. Your message was not sent.",
            SendResult.TooLarge => "That message is too large to send.",
            _ => "The message could not be sent. Please try again.",
        };
        return result == SendResult.AcceptedForSend;
    }

    private void TrimBufferedMessages()
    {
        while (MessageList.Count > MaxBufferedMessages)
        {
            MessageList.RemoveAt(0);
        }
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        GC.SuppressFinalize(this);
    }
}
