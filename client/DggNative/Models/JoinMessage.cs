using System.Text.Json.Serialization;
using DggNative.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(JsonFlattenConverter<JoinMessage>))]
public class JoinMessage : IWebSocketMessage
{
    [JsonFlatten]
    public required User User { get; init; }
    
    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }
}