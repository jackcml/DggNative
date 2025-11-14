using System;
using System.Text;
using DggNative.Models;

namespace DggNative.Services
{
    /// <summary>
    /// Handles parsing of WebSocket messages
    /// </summary>
    public static class MessageParser
    {
        /// <summary>
        /// Parse a raw message into a WebSocketMessage.
        /// Format looks like `TYPE <json_value>`
        /// </summary>
        public static WebSocketMessage ParseWebSocketMessage(byte[] buffer, int count)
        {
            var i = 0;
            while (i < count && buffer[i] != ' ')
            {
                i++;
            }
            var messageType = Encoding.UTF8.GetString(buffer, 0, i);
            var messageJson = Encoding.UTF8.GetString(buffer, i + 1, count - i - 1);

            return new WebSocketMessage(messageType, messageJson);
        }

        /// <summary>
        /// Serialize a WebSocketMessage
        /// </summary>
        public static string SerializeWebSocketMessage(WebSocketMessage message)
        {
            return message.Type + " " + message.Json;
        }
    }

    /// <summary>
    /// Exception thrown when message parsing fails
    /// </summary>
    public class MessageParseException : Exception
    {
        public MessageParseException(string message) : base(message) { }
        public MessageParseException(string message, Exception innerException) : base(message, innerException) { }
    }
}