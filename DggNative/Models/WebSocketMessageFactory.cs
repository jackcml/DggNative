using System;
using System.Text.Json;

namespace DggNative.Models;

public static class WebSocketMessageFactory
{
    public static IWebSocketMessage? Create(string messageType, ReadOnlySpan<byte> jsonBytes)
    {
        var reader = new Utf8JsonReader(jsonBytes);
        switch (messageType)
        {
            case "MSG":
                return JsonSerializer.Deserialize<ChatMessage>(ref reader);
            default:
                Console.WriteLine($"Unsupported message type `{messageType}`.");
                return null;
        }
    }
}