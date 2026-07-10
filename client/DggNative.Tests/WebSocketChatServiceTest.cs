using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Channels;
using DggNative.Models;
using DggNative.Services;

namespace DggNative.Tests;

public class WebSocketChatServiceTest
{
    private static readonly ChatServerConfig Config =
        new(new Uri("ws://localhost:8080"), ChatAuthMode.Hello);

    [Fact]
    public async Task StartReturnsImmediatelyAndStopAwaitsTheWorker()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);

        await service.StartAsync();
        await factory.CreatedSignal.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Single(factory.Sockets);

        await service.StopAsync();
        Assert.True(factory.Sockets[0].Disposed);
        Assert.Equal(WebSocketState.Closed, factory.Sockets[0].State);
    }

    [Fact]
    public async Task RepeatedStartAndStopNeverCreatesConcurrentWorkers()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.StartAsync()));
        Assert.Single(factory.Sockets);
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => service.StopAsync()));

        await service.StartAsync();
        Assert.Equal(2, factory.Sockets.Count);
        Assert.True(factory.Sockets[0].Disposed);
        await service.StopAsync();
    }

    [Fact]
    public async Task ConcurrentReconnectsAreSerializedByGeneration()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);
        await service.StartAsync();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => service.ReconnectAsync()));

        Assert.All(results, result => Assert.Equal(ConnectionAttemptResult.Connected, result));
        Assert.Equal(7, factory.Sockets.Count);
        Assert.Equal(1, factory.Sockets.Count(socket => !socket.Disposed));
    }

    [Fact]
    public async Task ReconnectReturnsBoundedFailureWhileWorkerEntersRetryDelay()
    {
        var factory = new FakeSocketFactory { FailConnect = true };
        await using var service = CreateService(factory);

        var result = await service.ReconnectAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(ConnectionAttemptResult.Failed, result);
        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StopCancelsAnInProgressConnect()
    {
        var factory = new FakeSocketFactory { BlockConnect = true };
        await using var service = CreateService(factory);
        await service.StartAsync();
        await factory.CreatedSignal.Task;

        await service.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(factory.Sockets[0].Disposed);
    }

    [Fact]
    public async Task SendsAreSerializedAndReturnExplicitResults()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);
        Assert.Equal(SendResult.NotReady, await service.SendMessageAsync("before start"));
        Assert.Equal(SendResult.TooLarge,
            await service.SendMessageAsync(new string('x', WebSocketChatService.MaxOutboundBytes + 1)));
        Assert.Equal(ConnectionAttemptResult.Connected, await service.ReconnectAsync());

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(index => service.SendMessageAsync($"MSG {index}")));

        Assert.All(results, result => Assert.Equal(SendResult.AcceptedForSend, result));
        Assert.Equal(1, factory.Sockets[^1].MaxConcurrentSends);
    }

    [Fact]
    public async Task ConfigurationIsCapturedByTheNextGeneration()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);
        var changed = Config with { ServerUri = new Uri("ws://example.test:9000") };

        await service.ConfigureAsync(changed);
        await service.ReconnectAsync();

        Assert.Equal(changed, factory.Configs.Single());
    }

    [Fact]
    public async Task ConcurrentConfigurationAndReconnectRequestsKeepOneActiveGeneration()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
        {
            await service.ConfigureAsync(Config with { ServerUri = new Uri($"ws://localhost:{9000 + index}") });
            await service.ReconnectAsync();
        }));

        Assert.Equal(1, factory.Sockets.Count(socket => !socket.Disposed));
    }

    [Fact]
    public async Task DisposalWaitsForAnActiveSendBeforeDisposingTheSocket()
    {
        var factory = new FakeSocketFactory { BlockSend = true };
        var service = CreateService(factory);
        await service.ReconnectAsync();
        var socket = factory.Sockets.Single();
        var send = service.SendMessageAsync("MSG active");
        await socket.SendStarted.Task;

        var disposal = service.DisposeAsync().AsTask();
        Assert.False(disposal.IsCompleted);
        Assert.False(socket.Disposed);

        socket.ReleaseSend.TrySetResult();
        Assert.Equal(SendResult.AcceptedForSend, await send);
        await disposal;
        Assert.True(socket.Disposed);
    }

    [Fact]
    public async Task DisposeStopsTrafficAndRejectsFurtherStarts()
    {
        var factory = new FakeSocketFactory();
        var service = CreateService(factory);
        await service.StartAsync();

        await service.DisposeAsync();

        Assert.True(factory.Sockets.Single().Disposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => service.StartAsync());
    }

    [Fact]
    public async Task SessionEntersGuestWithoutIdentityAndReadyOnlyAfterMe()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = service.SessionState.Subscribe(state =>
        {
            if (state is ChatSessionReady) ready.TrySetResult();
        });

        await service.ReconnectAsync();
        Assert.IsType<ChatSessionGuest>(await service.SessionState.FirstAsync());

        factory.Sockets.Single().EmitText(
            "ME {\"id\":1,\"nick\":\"jack\",\"roles\":[],\"features\":[],\"createdDate\":\"2026-01-01T00:00:00Z\",\"watching\":null,\"subscription\":null}");
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsType<ChatSessionReady>(await service.SessionState.FirstAsync());
    }

    [Fact]
    public async Task NickConflictRejectsIdentificationAndOnlyThenClearsTheNick()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory, Config with { HelloNick = "taken" });
        var rejected = new TaskCompletionSource<ChatSessionRejected>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = service.SessionState.OfType<ChatSessionRejected>()
            .Subscribe(state => rejected.TrySetResult(state));

        await service.ReconnectAsync();
        Assert.IsType<ChatSessionAuthenticating>(await service.SessionState.FirstAsync());
        factory.Sockets.Single().EmitText(
            "ERROR {\"code\":\"NICK_IN_USE\",\"message\":\"That nickname is already in use.\"}");

        Assert.Equal(NativeErrorCodes.NickInUse,
            (await rejected.Task.WaitAsync(TimeSpan.FromSeconds(1))).Code);
        await service.ReconnectAsync();
        Assert.Null(factory.Configs[^1].HelloNick);
        Assert.IsType<ChatSessionGuest>(await service.SessionState.FirstAsync());
    }

    [Fact]
    public async Task PolicyCloseWithoutExplicitConflictDoesNotClearTheNick()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory, Config with { HelloNick = "keep_me" });
        await service.ReconnectAsync();

        factory.Sockets.Single().EmitClose(
            WebSocketCloseStatus.PolicyViolation, "INVALID_MESSAGE");
        await service.ReconnectAsync();

        Assert.Equal("keep_me", factory.Configs[^1].HelloNick);
    }

    [Fact]
    public async Task IdentificationRequiredMapsBackToGuestWithoutBecomingNickConflict()
    {
        var factory = new FakeSocketFactory();
        await using var service = CreateService(factory, Config with { HelloNick = "jack" });
        var guest = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = service.SessionState.Subscribe(state =>
        {
            if (state is ChatSessionGuest) guest.TrySetResult();
        });
        await service.ReconnectAsync();

        factory.Sockets.Single().EmitText(
            "ERROR {\"code\":\"IDENTIFICATION_REQUIRED\",\"message\":\"Choose a nickname.\"}");

        await guest.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsType<ChatSessionGuest>(await service.SessionState.FirstAsync());
        Assert.Equal("jack", factory.Configs.Single().HelloNick);
    }

    private static WebSocketChatService CreateService(
        FakeSocketFactory factory, ChatServerConfig? config = null) =>
        new(config ?? Config, factory, new BlockingTimeProvider(), new FixedRandom());

    private sealed class FixedRandom : IReconnectRandom
    {
        public int Next(int minValue, int maxValue) => minValue;
    }

    private sealed class FakeSocketFactory : IChatWebSocketFactory
    {
        public List<FakeSocket> Sockets { get; } = [];
        public List<ChatServerConfig> Configs { get; } = [];
        public TaskCompletionSource CreatedSignal { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailConnect { get; init; }
        public bool BlockConnect { get; init; }
        public bool BlockSend { get; init; }

        public IChatWebSocket Create(ChatServerConfig config, AuthCookies? cookies)
        {
            var socket = new FakeSocket(FailConnect, BlockConnect, BlockSend);
            lock (Sockets) { Sockets.Add(socket); Configs.Add(config); }
            CreatedSignal.TrySetResult();
            return socket;
        }
    }

    private sealed class FakeSocket(bool failConnect, bool blockConnect, bool blockSend) : IChatWebSocket
    {
        private readonly Channel<ReceiveItem> _received = Channel.CreateUnbounded<ReceiveItem>();
        private int _activeSends;
        public WebSocketState State { get; private set; } = WebSocketState.None;
        public WebSocketCloseStatus? CloseStatus { get; private set; }
        public string? CloseStatusDescription { get; private set; }
        public bool Disposed { get; private set; }
        public int MaxConcurrentSends { get; private set; }
        public TaskCompletionSource SendStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseSend { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            State = WebSocketState.Connecting;
            if (blockConnect) await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            if (failConnect) throw new WebSocketException("simulated connect failure");
            State = WebSocketState.Open;
        }

        public async Task<WebSocketReceiveResult> ReceiveAsync(
            ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            var item = await _received.Reader.ReadAsync(cancellationToken);
            if (item.Type == WebSocketMessageType.Close)
                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            item.Bytes.AsSpan().CopyTo(buffer.AsSpan());
            return new WebSocketReceiveResult(item.Bytes.Length, item.Type, true);
        }

        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType,
            bool endOfMessage, CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _activeSends);
            MaxConcurrentSends = Math.Max(MaxConcurrentSends, active);
            SendStarted.TrySetResult();
            if (blockSend) await ReleaseSend.Task.WaitAsync(cancellationToken);
            else await Task.Yield();
            Interlocked.Decrement(ref _activeSends);
        }

        public Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription,
            CancellationToken cancellationToken)
        {
            State = WebSocketState.Closed;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            Disposed = true;
            State = WebSocketState.Closed;
        }

        public void EmitText(string frame) =>
            _received.Writer.TryWrite(new ReceiveItem(Encoding.UTF8.GetBytes(frame), WebSocketMessageType.Text));

        public void EmitClose(WebSocketCloseStatus status, string description)
        {
            CloseStatus = status;
            CloseStatusDescription = description;
            _received.Writer.TryWrite(new ReceiveItem([], WebSocketMessageType.Close));
        }

        private sealed record ReceiveItem(byte[] Bytes, WebSocketMessageType Type);
    }

    private sealed class BlockingTimeProvider : TimeProvider
    {
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime,
            TimeSpan period) => new BlockingTimer();

        private sealed class BlockingTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
