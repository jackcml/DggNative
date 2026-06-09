using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.IO;
using DggNative.Models;

namespace DggNative.Services;

public class WebSocketChatService(Uri serverUri) : IChatService, IDisposable
{
    private readonly BehaviorSubject<ConnectionStatus> _connectionState = new(new ConnectionStatusDisconnected());
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource _cancellationTokenSource = new();
    private readonly Subject<IWebSocketMessage> _messageSubject = new();
    private int _retryAttempts;
    private AuthCookies? _authCookies;
    private Task? _connectTask;

    public IObservable<IWebSocketMessage> MessageStream => _messageSubject.AsObservable();
    public IObservable<ConnectionStatus> IsConnected => _connectionState.AsObservable();

    public async Task ConnectAsync()
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                _connectionState.OnNext(new ConnectionStatusConnecting());

                _webSocket?.Dispose();
                _webSocket = new ClientWebSocket();

                // Use externally-provided auth cookies, falling back to env vars
                var sid = _authCookies?.Sid ?? Environment.GetEnvironmentVariable("sid");
                var rememberme = _authCookies?.RememberMe ?? Environment.GetEnvironmentVariable("rememberme");

                if (!string.IsNullOrEmpty(sid) || !string.IsNullOrEmpty(rememberme))
                {
                    var cookieContainer = new CookieContainer();

                    if (!string.IsNullOrEmpty(sid))
                        cookieContainer.Add(new Cookie("sid", sid, "/", serverUri.Host));

                    if (!string.IsNullOrEmpty(rememberme))
                        cookieContainer.Add(new Cookie("rememberme", rememberme, "/", serverUri.Host));

                    _webSocket.Options.Cookies = cookieContainer;
                }

                _webSocket.Options.SetRequestHeader("Origin", "https://www.destiny.gg");

                await _webSocket.ConnectAsync(serverUri, _cancellationTokenSource.Token);

                _connectionState.OnNext(new ConnectionStatusConnected());
                _retryAttempts = 0;

                await ReceiveLoopAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Ignored, will retry
            }

            if (_cancellationTokenSource.IsCancellationRequested) break;

            _connectionState.OnNext(new ConnectionStatusDisconnected());

            // Backoff logic
            var delayMs = _retryAttempts == 0
                ? Random.Shared.Next(501, 3001)
                : Random.Shared.Next(5000, 30001);

            _retryAttempts++;

            // Countdown loop
            while (delayMs > 0)
            {
                _connectionState.OnNext(new ConnectionStatusRetrying(delayMs));

                const int stepMs = 250;
                await Task.Delay(Math.Min(stepMs, delayMs), _cancellationTokenSource.Token);
                delayMs -= stepMs;

                if (_cancellationTokenSource.IsCancellationRequested) break;
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_webSocket == null) return;

        var buffer = new byte[8192];
        using var ms = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
            catch
            {
                break; // Connection failed
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text) continue;

            ms.Write(buffer, 0, result.Count);

            if (!result.EndOfMessage) continue;

            ReadOnlySpan<byte> messageBytes = ms.ToArray().AsSpan();
            ms.SetLength(0); // Reset for next message

            var spaceIndex = messageBytes.IndexOf((byte)' ');
            if (spaceIndex == -1) continue; // Invalid message format

            var objectType = Encoding.UTF8.GetString(messageBytes[..spaceIndex]);

            try
            {
                // Parse remaining buffer data (JSON) into corresponding IWebSocketMessage type
                var message = WebSocketMessageFactory.Create(objectType, messageBytes[(spaceIndex + 1)..]);

                // Push message into stream
                if (message != null)
                {
                    _messageSubject.OnNext(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing message: {ex.Message}");
            }
        }
    }

    public async Task StartAsync()
    {
        _connectTask = ConnectAsync();
        await _connectTask;
    }

    public void SetAuthCookies(AuthCookies? cookies)
    {
        _authCookies = cookies;
    }

    public async Task ReconnectAsync()
    {
        await DisconnectAsync();

        // Wait for the previous ConnectAsync loop to fully exit
        if (_connectTask != null)
        {
            try { await _connectTask; }
            catch { /* already handled */ }
        }

        _cancellationTokenSource = new CancellationTokenSource();
        _connectTask = ConnectAsync();
        await _connectTask;
    }

    public async Task DisconnectAsync()
    {
        await _cancellationTokenSource.CancelAsync();

        if (_webSocket is { State: WebSocketState.Open })
        {
            try
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
            }
            catch
            {
                // Ignore close errors
            }
        }
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        if (_webSocket is not { State: WebSocketState.Open }) return;

        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _webSocket?.Dispose();
        _messageSubject.Dispose();
        _connectionState.Dispose();
    }
}