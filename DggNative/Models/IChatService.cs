using System;
using System.Threading;
using System.Threading.Tasks;

namespace DggNative.Models;

public interface IChatService
{
    // Connection and sending methods
    Task ConnectAsync();
    Task DisconnectAsync();
    Task SendMessageAsync(string message, CancellationToken cancellationToken);
    
    // Stream of all messages received from the websocket, parsed into objects
    IObservable<IWebSocketMessage> MessageStream { get; }
}