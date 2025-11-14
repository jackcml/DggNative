using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DggNative.Models;

namespace DggNative.Services
{
    /// <summary>
    /// Manages WebSocket connection and message handling
    /// </summary>
    public class WebSocketService
    {
        private ClientWebSocket _webSocket;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly string _serverUrl;
        private readonly string _sessionToken;
        private bool _isConnected;

        public event EventHandler<ChatMessage> MessageReceived;
        public event EventHandler<string> ConnectionStatusChanged;
        public event EventHandler<Exception> ErrorOccurred;

        public bool IsConnected => _isConnected;

        public WebSocketService(string serverUrl, string sessionToken)
        {
            _serverUrl = serverUrl;
            _sessionToken = sessionToken;
            _isConnected = false;
        }

        /// <summary>
        /// Establish WebSocket connection with session cookie
        /// </summary>
        public async Task ConnectAsync()
        {
            try
            {
                if (_isConnected)
                    return;

                _webSocket = new ClientWebSocket();
                _cancellationTokenSource = new CancellationTokenSource();

                // Set session cookie for authentication
                _webSocket.Options.SetRequestHeader("Cookie", $"session={_sessionToken}");

                OnConnectionStatusChanged("Connecting...");

                await _webSocket.ConnectAsync(new Uri(_serverUrl), _cancellationTokenSource.Token);

                _isConnected = true;
                OnConnectionStatusChanged("Connected");

                // Start listening for messages
                _ = Task.Run(() => ListenForMessagesAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
                OnConnectionStatusChanged("Connection failed");
                _isConnected = false;
            }
        }

        /// <summary>
        /// Disconnect from WebSocket server
        /// </summary>
        public async Task DisconnectAsync()
        {
            try
            {
                if (!_isConnected)
                    return;

                _isConnected = false;
                _cancellationTokenSource?.Cancel();

                if (_webSocket != null && _webSocket.State == WebSocketState.Open)
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
                }

                OnConnectionStatusChanged("Disconnected");
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
            finally
            {
                _webSocket?.Dispose();
                _webSocket = null;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// Send a message to the server
        /// </summary>
        public async Task SendMessageAsync(WebSocketMessage message)
        {
            try
            {
                if (!_isConnected || _webSocket?.State != WebSocketState.Open)
                {
                    throw new InvalidOperationException("Not connected to server");
                }

                var message_str = MessageParser.SerializeWebSocketMessage(message);
                var buffer = Encoding.UTF8.GetBytes(message_str);

                await _webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        /// <summary>
        /// Listen for incoming messages from the server
        /// </summary>
        private async Task ListenForMessagesAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];

            try
            {
                while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
                {
                    var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        if (result.Count == 0) continue;
                        WebSocketMessage message = MessageParser.ParseWebSocketMessage(buffer, result.Count);
                        ProcessMessage(message);
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation during disconnect
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
            finally
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    OnConnectionStatusChanged("Disconnected");
                }
            }
        }

        /// <summary>
        /// Process incoming message from server
        /// </summary>
        private void ProcessMessage(WebSocketMessage message)
        {
            try
            {
                switch (message.Type)
                {
                    case "MSG":
                        var chatMessage = ChatMessage.Deserialize(message.Json);
                        OnMessageReceived(chatMessage);
                        break;
                    // Handle other message types as needed
                    default:
                        Console.WriteLine($"Unsupported message type `{message.Type}`.");
                        break;
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex);
            }
        }

        protected virtual void OnMessageReceived(ChatMessage message)
        {
            MessageReceived?.Invoke(this, message);
        }

        protected virtual void OnConnectionStatusChanged(string status)
        {
            ConnectionStatusChanged?.Invoke(this, status);
        }

        protected virtual void OnErrorOccurred(Exception ex)
        {
            ErrorOccurred?.Invoke(this, ex);
        }
    }
}