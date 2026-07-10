using System.Text.Json.Serialization;

namespace DggNative.Models;

public sealed class ErrorMessage : IWebSocketMessage
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
