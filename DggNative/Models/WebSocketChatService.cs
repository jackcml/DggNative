using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace DggNative.Models;

public class WebSocketChatService(Uri serverUri) : IChatService, IDisposable
{
    private readonly ClientWebSocket _webSocket = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Subject<IWebSocketMessage> _messageSubject = new();
    private Task? _receiveLoopTask;

    public IObservable<IWebSocketMessage> MessageStream => _messageSubject.AsObservable();

    public async Task ConnectAsync()
    {
        await _webSocket.ConnectAsync(serverUri, _cancellationTokenSource.Token);
        _receiveLoopTask = ReceiveLoopAsync(_cancellationTokenSource.Token);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        // NOTE: 8K message length limit, assuming single frame
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                break;
            }

            if (result.MessageType != WebSocketMessageType.Text) continue;
            // Get object type from first word of buffer
            ReadOnlySpan<byte> messageBytes = buffer.AsSpan(0, result.Count);
            var spaceIndex = messageBytes.IndexOf((byte)' ');
            var objectType = Encoding.UTF8.GetString(messageBytes[..spaceIndex]);

            // Parse remaining buffer data (JSON) into corresponding IWebSocketMessage type
            var message = WebSocketMessageFactory.Create(objectType, messageBytes[(spaceIndex + 1)..]);

            // Push message into stream
            if (message != null)
            {
                _messageSubject.OnNext(message);
            }
        }
    }
    
    public async Task DisconnectAsync()
    {
        await _cancellationTokenSource.CancelAsync();

        if (_webSocket.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }
        
        // Wait for receive loop to complete
        if (_receiveLoopTask != null)
        {
            await _receiveLoopTask;
        }
    }
    
    public async Task SendMessageAsync(string message, CancellationToken cancellationToken)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _webSocket.Dispose();
        _messageSubject.Dispose();
    }
}