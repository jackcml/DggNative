using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DggNative.Models;

public class NamesMessage : IWebSocketMessage
{
    [JsonPropertyName("connectioncount")] public required int ConnectionCount { get; init; }

    [JsonPropertyName("users")] public required List<User> Users { get; init; }
}
