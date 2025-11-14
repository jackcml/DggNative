using System;
using Gtk;
using DggNative.Models;

namespace DggNative.UI
{
    /// <summary>
    /// Component for handling message input and sending
    /// </summary>
    public class InputArea
    {
        private readonly Entry _messageEntry;
        private readonly Button _sendButton;
        private readonly string _username;
        private bool _isEnabled;

        public event EventHandler<ChatMessage> MessageSendRequested;

        /// <summary>
        /// Gets or sets whether the input area is enabled
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                _messageEntry.Sensitive = value;
                _sendButton.Sensitive = value;
            }
        }

        public InputArea(Entry messageEntry, Button sendButton, string username)
        {
            _messageEntry = messageEntry;
            _sendButton = sendButton;
            _username = username;
            _isEnabled = false;

            // Connect events
            _sendButton.Clicked += OnSendButtonClicked;
            _messageEntry.Activated += OnMessageEntryActivated;

            // Initial state
            IsEnabled = false;
        }

        /// <summary>
        /// Clear the input field
        /// </summary>
        public void ClearInput()
        {
            _messageEntry.Text = string.Empty;
            _messageEntry.GrabFocus();
        }

        /// <summary>
        /// Set focus to the input field
        /// </summary>
        public void FocusInput()
        {
            _messageEntry.GrabFocus();
        }

        /// <summary>
        /// Handle send button click
        /// </summary>
        private void OnSendButtonClicked(object sender, EventArgs e)
        {
            SendMessage();
        }

        /// <summary>
        /// Handle Enter key in the input field
        /// </summary>
        private void OnMessageEntryActivated(object sender, EventArgs e)
        {
            SendMessage();
        }

        /// <summary>
        /// Send the current message
        /// </summary>
        private void SendMessage()
        {
            var messageText = _messageEntry.Text?.Trim();

            // Validate message
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return;
            }

            // Create chat message
            // FIXME: we don't have a reference to the current user here
            var message = new ChatMessage(new User(0, _username, null, null, "", null, null), messageText, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            // Raise event
            MessageSendRequested?.Invoke(this, message);

            // Clear input
            ClearInput();
        }
    }
}