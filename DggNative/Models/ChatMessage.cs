using System.Text.Json.Serialization;
using DggNative.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(JsonFlattenConverter<ChatMessage>))]
public class ChatMessage : IWebSocketMessage
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonFlatten]
    public required User User { get; set; }
    
    [JsonPropertyName("data")]
    public required string Data { get; set; }
}