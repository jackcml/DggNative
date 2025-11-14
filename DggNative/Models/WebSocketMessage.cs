namespace DggNative.Models
{
    /// <summary>
    /// Represents a websocket message with type and json value
    /// </summary>
    public class WebSocketMessage
    {
        public string Type { get; }
        public string Json { get; }

        public WebSocketMessage(string type, string json)
        {
            this.Type = type;
            this.Json = json;
        }
    }
}