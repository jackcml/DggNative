using System.Text.Json.Serialization;
using DggNative.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(JsonFlattenConverter<MeMessage>))]
public class MeMessage : IWebSocketMessage
{
    [JsonFlatten] public required User User { get; init; }
}