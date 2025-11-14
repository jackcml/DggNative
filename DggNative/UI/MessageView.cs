using System;
using Gtk;
using DggNative.Models;

namespace DggNative.UI
{
    /// <summary>
    /// Component for displaying chat messages in a ListBox
    /// </summary>
    public class MessageView
    {
        private readonly ListBox _messageListBox;
        private readonly string _currentUsername;

        public MessageView(ListBox messageListBox, string currentUsername = null)
        {
            if (messageListBox == null)
            {
                throw new ArgumentNullException(nameof(messageListBox), "MessageListBox cannot be null");
            }

            _messageListBox = messageListBox;
            _currentUsername = currentUsername;

            // Configure ListBox
            _messageListBox.SelectionMode = SelectionMode.None;
            _messageListBox.HeaderFunc = UpdateHeader;
        }

        /// <summary>
        /// Add a new chat message to the view
        /// </summary>
        public void AddChatMessage(ChatMessage message)
        {
            var messageWidget = CreateMessageWidget(message);
            _messageListBox.Add(messageWidget);
            messageWidget.ShowAll();

            // Scroll to bottom if needed
            var parent = _messageListBox.Parent as ScrolledWindow;
            if (parent != null)
            {
                var vAdjustment = parent.Vadjustment;
                vAdjustment.Value = vAdjustment.Upper - vAdjustment.PageSize;
            }
        }

        /// <summary>
        /// Clear all messages from the view
        /// </summary>
        public void ClearMessages()
        {
            foreach (Widget child in _messageListBox.Children)
            {
                _messageListBox.Remove(child);
                child.Destroy();
            }
        }

        /// <summary>
        /// Create a widget for displaying a single chat message
        /// </summary>
        private Widget CreateMessageWidget(ChatMessage message)
        {
            var messageBox = new Box(Orientation.Vertical, 2);

            // Header with username and timestamp
            var headerBox = new Box(Orientation.Horizontal, 5);

            var usernameLabel = new Label(message.user.nick);
            usernameLabel.Xalign = 0;

            // Style own messages differently
            if (message.user.nick == _currentUsername)
            {
                usernameLabel.Markup = $"<b>{message.user.nick}</b>";
            }
            else
            {
                usernameLabel.Markup = $"<span color=\"#2e6da4\">{message.user.nick}</span>";
            }

            var timestampLabel = new Label(message.timestamp.ToString("HH:mm"));
            timestampLabel.Xalign = 1;

            headerBox.PackStart(usernameLabel, true, true, 0);
            headerBox.PackEnd(timestampLabel, false, false, 0);

            // Message content
            var contentLabel = new Label(message.data);
            contentLabel.Xalign = 0;
            contentLabel.Wrap = true;
            contentLabel.Selectable = true;

            messageBox.PackStart(headerBox, false, false, 0);
            messageBox.PackStart(contentLabel, false, false, 0);

            return messageBox;
        }

        /// <summary>
        /// Update headers for message grouping by date
        /// </summary>
        private void UpdateHeader(ListBoxRow row, ListBoxRow before)
        {
            if (row == null)
                return;

            var messageBox = row.Child as Box;
            if (messageBox == null)
                return;

            // Remove existing header
            var existingHeader = row.Header as Label;
            if (existingHeader != null)
            {
                row.Header = null;
                existingHeader.Destroy();
            }

            // Add date header if this is the first message or date changed
            if (before == null)
            {
                AddDateHeader(row, GetMessageDate(row));
            }
            else
            {
                var currentDate = GetMessageDate(row);
                var previousDate = GetMessageDate(before);

                if (currentDate.Date != previousDate.Date)
                {
                    AddDateHeader(row, currentDate);
                }
            }
        }

        /// <summary>
        /// Add a date header to a row
        /// </summary>
        private void AddDateHeader(ListBoxRow row, DateTime date)
        {
            var headerLabel = new Label(date.ToString("MMMM d, yyyy"));
            headerLabel.Xalign = 0.5f;
            row.Header = headerLabel;
        }

        /// <summary>
        /// Extract message timestamp from a row
        /// </summary>
        private DateTime GetMessageDate(ListBoxRow row)
        {
            // This is a simplified approach - in a real implementation,
            // we'd store the message object with the row
            return DateTime.Now;
        }
    }
}