using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public interface IChatWebSocket : IDisposable
{
    WebSocketState State { get; }
    WebSocketCloseStatus? CloseStatus { get; }
    string? CloseStatusDescription { get; }
    Task ConnectAsync(Uri uri, CancellationToken cancellationToken);
    Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken);
    Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
        CancellationToken cancellationToken);
    Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
        CancellationToken cancellationToken);
}

public interface IChatWebSocketFactory
{
    IChatWebSocket Create(ChatServerConfig config, AuthCookies? cookies);
}

public sealed class ChatWebSocketFactory : IChatWebSocketFactory
{
    public IChatWebSocket Create(ChatServerConfig config, AuthCookies? cookies)
    {
        if (config.AuthMode == ChatAuthMode.Cookie
            && !EndpointTrust.IsApprovedOfficialChatEndpoint(config.ServerUri))
            throw new InvalidOperationException("Cookie authentication is restricted to the approved official wss:// endpoint.");

        var socket = new ClientWebSocket();
        if (config.AuthMode == ChatAuthMode.Cookie)
        {
            var sid = cookies?.Sid ?? Environment.GetEnvironmentVariable("sid");
            var rememberMe = cookies?.RememberMe ?? Environment.GetEnvironmentVariable("rememberme");
            if (!string.IsNullOrEmpty(sid) || !string.IsNullOrEmpty(rememberMe))
            {
                var cookieContainer = new CookieContainer();
                if (!string.IsNullOrEmpty(sid))
                    cookieContainer.Add(new Cookie("sid", sid, "/", config.ServerUri.Host));
                if (!string.IsNullOrEmpty(rememberMe))
                    cookieContainer.Add(new Cookie("rememberme", rememberMe, "/", config.ServerUri.Host));
                socket.Options.Cookies = cookieContainer;
            }
            socket.Options.SetRequestHeader("Origin", "https://www.destiny.gg");
        }
        return new ClientWebSocketAdapter(socket);
    }

    private sealed class ClientWebSocketAdapter(ClientWebSocket socket) : IChatWebSocket
    {
        public WebSocketState State => socket.State;
        public WebSocketCloseStatus? CloseStatus => socket.CloseStatus;
        public string? CloseStatusDescription => socket.CloseStatusDescription;
        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
            socket.ConnectAsync(uri, cancellationToken);
        public Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) =>
            socket.ReceiveAsync(buffer, cancellationToken);
        public Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage,
            CancellationToken cancellationToken) => socket.SendAsync(buffer, messageType, endOfMessage, cancellationToken);
        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken) => socket.CloseAsync(closeStatus, statusDescription, cancellationToken);
        public void Dispose() => socket.Dispose();
    }
}

public interface IReconnectRandom
{
    int Next(int minValue, int maxValue);
}

public sealed class SharedReconnectRandom : IReconnectRandom
{
    public int Next(int minValue, int maxValue) => Random.Shared.Next(minValue, maxValue);
}

public interface IReconnectPolicy
{
    TimeSpan GetDelay(int retryAttempt, IReconnectRandom random);
}

public sealed class DefaultReconnectPolicy : IReconnectPolicy
{
    public TimeSpan GetDelay(int retryAttempt, IReconnectRandom random) => TimeSpan.FromMilliseconds(
        retryAttempt == 0 ? random.Next(501, 3001) : random.Next(5000, 30001));
}

public sealed record ChatConnectionFailure(string Operation, string Category, Exception Exception);
