using System;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public enum SendResult
{
    AcceptedForSend,
    NotReady,
    TooLarge,
    Failed,
}

public enum ConnectionAttemptResult
{
    Connected,
    Failed,
    Stopped,
}

public interface IChatService : IDisposable, IAsyncDisposable
{
    // Starts the background worker and returns without waiting for a connection.
    Task StartAsync(CancellationToken cancellationToken = default);
    // Replaces the worker and completes after its first connect attempt has a bounded outcome.
    Task<ConnectionAttemptResult> ReconnectAsync(CancellationToken cancellationToken = default);
    // Cancels and awaits the exact active worker generation.
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<SendResult> SendMessageAsync(string message, CancellationToken cancellationToken = default);
    Task ConfigureAsync(ChatServerConfig config, CancellationToken cancellationToken = default);
    void SetAuthCookies(AuthCookies? cookies);

    IObservable<IWebSocketMessage> MessageStream { get; }
    IObservable<ConnectionStatus> IsConnected { get; }
}
