using System;
using System.IO;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public sealed class WebSocketChatService : IChatService
{
    public const int MaxOutboundBytes = 16 * 1024;

    private readonly BehaviorSubject<ConnectionStatus> _connectionState =
        new(new ConnectionStatusDisconnected());
    private readonly Subject<IWebSocketMessage> _messageSubject = new();
    private readonly SemaphoreSlim _controlGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly object _settingsGate = new();
    private readonly IChatWebSocketFactory _socketFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IReconnectRandom _random;
    private readonly IReconnectPolicy _reconnectPolicy;
    private readonly Action<ChatConnectionFailure>? _failureSink;

    private ChatServerConfig _config;
    private AuthCookies? _authCookies;
    private WorkerGeneration? _worker;
    private IChatWebSocket? _activeSocket;
    private bool _disposed;

    public WebSocketChatService(ChatServerConfig config)
        : this(config, new ChatWebSocketFactory(), TimeProvider.System, new SharedReconnectRandom()) { }

    public WebSocketChatService(
        ChatServerConfig config,
        IChatWebSocketFactory socketFactory,
        TimeProvider timeProvider,
        IReconnectRandom random,
        IReconnectPolicy? reconnectPolicy = null,
        Action<ChatConnectionFailure>? failureSink = null)
    {
        _config = config;
        _socketFactory = socketFactory;
        _timeProvider = timeProvider;
        _random = random;
        _reconnectPolicy = reconnectPolicy ?? new DefaultReconnectPolicy();
        _failureSink = failureSink ?? LogFailure;
    }

    public IObservable<IWebSocketMessage> MessageStream => _messageSubject.AsObservable();
    public IObservable<ConnectionStatus> IsConnected => _connectionState.AsObservable();

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_worker is { Task.IsCompleted: false }) return;
            _worker = StartWorker();
        }
        finally { _controlGate.Release(); }
    }

    public async Task<ConnectionAttemptResult> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        Task<ConnectionAttemptResult> firstAttempt;
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await StopWorkerAsync().ConfigureAwait(false);
            _worker = StartWorker();
            firstAttempt = _worker.FirstAttempt.Task;
        }
        finally { _controlGate.Release(); }

        return await firstAttempt.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopWorkerAsync().ConfigureAwait(false);
            _connectionState.OnNext(new ConnectionStatusDisconnected());
        }
        finally { _controlGate.Release(); }
    }

    public async Task ConfigureAsync(ChatServerConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await _controlGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            lock (_settingsGate) _config = config;
        }
        finally { _controlGate.Release(); }
    }

    public void SetAuthCookies(AuthCookies? cookies)
    {
        lock (_settingsGate) _authCookies = cookies;
    }

    public async Task<SendResult> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        if (Encoding.UTF8.GetByteCount(message) > MaxOutboundBytes) return SendResult.TooLarge;
        var bytes = Encoding.UTF8.GetBytes(message);

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var socket = Volatile.Read(ref _activeSocket);
            if (socket is not { State: WebSocketState.Open }) return SendResult.NotReady;
            try
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken)
                    .ConfigureAwait(false);
                return SendResult.AcceptedForSend;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                ReportFailure("send", ex);
                return SendResult.Failed;
            }
        }
        finally { _sendGate.Release(); }
    }

    private WorkerGeneration StartWorker()
    {
        var cts = new CancellationTokenSource();
        var firstAttempt = new TaskCompletionSource<ConnectionAttemptResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var task = RunWorkerAsync(cts.Token, firstAttempt);
        return new WorkerGeneration(cts, task, firstAttempt);
    }

    private async Task RunWorkerAsync(
        CancellationToken cancellationToken,
        TaskCompletionSource<ConnectionAttemptResult> firstAttempt)
    {
        var retryAttempts = 0;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                IChatWebSocket? socket = null;
                try
                {
                    _connectionState.OnNext(new ConnectionStatusConnecting());
                    ChatServerConfig config;
                    AuthCookies? cookies;
                    lock (_settingsGate) { config = _config; cookies = _authCookies; }

                    socket = _socketFactory.Create(config, cookies);
                    Volatile.Write(ref _activeSocket, socket);
                    await socket.ConnectAsync(config.ServerUri, cancellationToken).ConfigureAwait(false);
                    _connectionState.OnNext(new ConnectionStatusConnected());
                    firstAttempt.TrySetResult(ConnectionAttemptResult.Connected);
                    retryAttempts = 0;

                    if (config is { AuthMode: ChatAuthMode.Hello, HelloNick: not null })
                    {
                        var hello = Encoding.UTF8.GetBytes(OutboundFrames.Hello(config.HelloNick));
                        await SendOnSocketAsync(socket, hello, cancellationToken).ConfigureAwait(false);
                    }

                    await ReceiveLoopAsync(socket, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    firstAttempt.TrySetResult(ConnectionAttemptResult.Failed);
                    ReportFailure("connection", ex);
                }
                finally
                {
                    Interlocked.CompareExchange(ref _activeSocket, null, socket);
                    if (socket != null)
                    {
                        await _sendGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                        try { socket.Dispose(); }
                        finally { _sendGate.Release(); }
                    }
                }

                if (cancellationToken.IsCancellationRequested) break;
                _connectionState.OnNext(new ConnectionStatusDisconnected());
                var delayMs = checked((int)_reconnectPolicy.GetDelay(retryAttempts, _random).TotalMilliseconds);
                retryAttempts++;
                await DelayWithCountdownAsync(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally
        {
            firstAttempt.TrySetResult(ConnectionAttemptResult.Stopped);
        }
    }

    private async Task ReceiveLoopAsync(IChatWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        using var stream = new MemoryStream();
        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.CloseStatus == WebSocketCloseStatus.PolicyViolation)
                {
                    lock (_settingsGate) _config = _config with { HelloNick = null };
                    _connectionState.OnNext(new ConnectionStatusRejected(
                        socket.CloseStatusDescription ?? "Rejected by server"));
                }
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) { ReportFailure("close", ex); }
                return;
            }
            if (result.MessageType != WebSocketMessageType.Text) continue;
            stream.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage) continue;
            ParseMessage(stream.ToArray());
            stream.SetLength(0);
        }
    }

    private void ParseMessage(byte[] bytes)
    {
        var spaceIndex = Array.IndexOf(bytes, (byte)' ');
        if (spaceIndex < 0) return;
        var type = Encoding.UTF8.GetString(bytes.AsSpan(0, spaceIndex));
        try
        {
            var message = WebSocketMessageFactory.Create(type, bytes.AsSpan(spaceIndex + 1));
            if (message != null) _messageSubject.OnNext(message);
        }
        catch (Exception ex) { ReportFailure("parse", ex); }
    }

    private async Task DelayWithCountdownAsync(int delayMs, CancellationToken cancellationToken)
    {
        while (delayMs > 0)
        {
            _connectionState.OnNext(new ConnectionStatusRetrying(delayMs));
            var step = Math.Min(250, delayMs);
            await Task.Delay(TimeSpan.FromMilliseconds(step), _timeProvider, cancellationToken)
                .ConfigureAwait(false);
            delayMs -= step;
        }
    }

    private async Task SendOnSocketAsync(IChatWebSocket socket, byte[] bytes, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
        }
        finally { _sendGate.Release(); }
    }

    private async Task StopWorkerAsync()
    {
        var worker = _worker;
        _worker = null;
        if (worker == null) return;
        await worker.Cancellation.CancelAsync().ConfigureAwait(false);
        await worker.Task.ConfigureAwait(false);
        worker.Cancellation.Dispose();
    }

    private void ReportFailure(string operation, Exception exception) =>
        _failureSink?.Invoke(new ChatConnectionFailure(operation, exception.GetType().Name, exception));

    private static void LogFailure(ChatConnectionFailure failure) =>
        Console.Error.WriteLine($"chat_connection_failure operation={failure.Operation} category={failure.Category}");

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _messageSubject.OnCompleted();
        _connectionState.OnCompleted();
        _messageSubject.Dispose();
        _connectionState.Dispose();
        _controlGate.Dispose();
        _sendGate.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private sealed record WorkerGeneration(
        CancellationTokenSource Cancellation,
        Task Task,
        TaskCompletionSource<ConnectionAttemptResult> FirstAttempt);
}
