using System.Net.WebSockets;
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

    private static WebSocketChatService CreateService(FakeSocketFactory factory) =>
        new(Config, factory, new BlockingTimeProvider(), new FixedRandom());

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
        private int _activeSends;
        public WebSocketState State { get; private set; } = WebSocketState.None;
        public WebSocketCloseStatus? CloseStatus => null;
        public string? CloseStatusDescription => null;
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
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable after infinite delay.");
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
