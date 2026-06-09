using System;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services;

public interface IChatService
{
    // Connection and sending methods
    Task ConnectAsync();
    Task DisconnectAsync();
    Task SendMessageAsync(string message, CancellationToken cancellationToken);
    void Configure(ChatServerConfig config);

    // Stream of all messages received from the websocket, parsed into objects
    IObservable<IWebSocketMessage> MessageStream { get; }
    IObservable<ConnectionStatus> IsConnected { get; }
}