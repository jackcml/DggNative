using System.Text.Json.Serialization;
using DggNative.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(JsonFlattenConverter<ChatMessage>))]
public class ChatMessage : IWebSocketMessage
{
    [JsonFlatten] public required User User { get; set; }
    [JsonPropertyName("content")] public required string Content { get; set; }
}