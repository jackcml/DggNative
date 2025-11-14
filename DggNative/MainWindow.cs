using System;
using Gtk;
using UIAttribute = Gtk.Builder.ObjectAttribute;
using DggNative.Services;
using DggNative.Models;
using DggNative.UI;
using DggNative.Utils;

namespace DggNative
{
    class MainWindow : Window
    {
        [UIAttribute] private Label _titleLabel = null;
        [UIAttribute] private Label _statusLabel = null;
        [UIAttribute] private ScrolledWindow _messageScrolledWindow = null;
        [UIAttribute] private ListBox _messageListBox = null;
        [UIAttribute] private Entry _messageEntry = null;
        [UIAttribute] private Button _sendButton = null;

        private WebSocketService _webSocketService;
        private MessageView _messageView;
        private InputArea _inputArea;
        private ScrollManager _scrollManager;
        private readonly string _serverUrl = "ws://localhost:8080"; // FIXME: Test url, should be configurable
        private readonly string _sessionToken = "DEV_SESSION_TOKEN_123";
        private readonly string _username = "User";

        public MainWindow() : base("Chat Application")
        {
            DefaultWidth = 800;
            DefaultHeight = 600;
            WidthRequest = 400;
            HeightRequest = 300;

            try
            {
                Builder builder = new Builder();
                builder.AddFromFile("MainWindow.glade");

                // Connect UI components
                _titleLabel = (Label)builder.GetObject("titleLabel");
                _statusLabel = (Label)builder.GetObject("statusLabel");
                _messageScrolledWindow = (ScrolledWindow)builder.GetObject("messageScrolledWindow");
                _messageListBox = (ListBox)builder.GetObject("messageListBox");
                _messageEntry = (Entry)builder.GetObject("messageEntry");
                _sendButton = (Button)builder.GetObject("sendButton");

                // Get the main window from builder
                var window = (Window)builder.GetObject("MainWindow");
                if (window != null)
                {
                    // Transfer child widgets from the builder window to this window
                    var child = window.Child;
                    if (child != null)
                    {
                        window.Remove(child);
                        this.Add(child);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading Glade file: {ex.Message}");
                throw;
            }

            Setup();
        }

        private void Setup()
        {
            // Set window properties
            Title = "Chat Application";
            DeleteEvent += Window_DeleteEvent;

            // Initialize UI components
            _messageView = new MessageView(_messageListBox, _username);
            _inputArea = new InputArea(_messageEntry, _sendButton, _username);
            _scrollManager = new ScrollManager(_messageScrolledWindow);

            // Initialize WebSocket service
            _webSocketService = new WebSocketService(_serverUrl, _sessionToken);
            _webSocketService.MessageReceived += OnMessageReceived;
            _webSocketService.ConnectionStatusChanged += OnConnectionStatusChanged;
            _webSocketService.ErrorOccurred += OnErrorOccurred;

            // Connect input area events
            _inputArea.MessageSendRequested += OnMessageSendRequested;

            // Connect to WebSocket server
            _ = ConnectWebSocketAsync();
        }

        private async System.Threading.Tasks.Task ConnectWebSocketAsync()
        {
            try
            {
                await _webSocketService.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
        }

        private void OnMessageReceived(object sender, ChatMessage message)
        {
            // Ensure UI updates happen on the main thread
            Application.Invoke((s, e) =>
            {
                _messageView.AddChatMessage(message);

                // Auto-scroll if at bottom
                if (_scrollManager.ShouldAutoScroll)
                {
                    _scrollManager.ScrollToBottom();
                }
                else
                {
                    _scrollManager.UpdateScrollPosition();
                }
            });
        }

        private void OnConnectionStatusChanged(object sender, string status)
        {
            Application.Invoke((s, e) =>
            {
                _statusLabel.Text = status;
                _inputArea.IsEnabled = status == "Connected";

                if (status == "Connected")
                {
                    _inputArea.FocusInput();
                }
            });
        }

        private void OnErrorOccurred(object sender, Exception ex)
        {
            Application.Invoke((s, e) =>
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
                // Could show error dialog here
            });
        }

        private async void OnMessageSendRequested(object sender, ChatMessage message)
        {
            try
            {
                await _webSocketService.SendMessageAsync(new WebSocketMessage("MSG", message.Serialize()));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex.Message}");
                // Could show error dialog here
            }
        }

        private void Window_DeleteEvent(object sender, DeleteEventArgs a)
        {
            // Clean up WebSocket connection
            _ = _webSocketService.DisconnectAsync();
            Application.Quit();
        }
    }
}
