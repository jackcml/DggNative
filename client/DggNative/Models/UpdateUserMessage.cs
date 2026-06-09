using System.Text.Json.Serialization;
using DggNative.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(JsonFlattenConverter<UpdateUserMessage>))]
public class UpdateUserMessage : IWebSocketMessage
{
    [JsonFlatten]
    public required User User { get; init; }
}