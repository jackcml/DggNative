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
            case "JOIN":
                return JsonSerializer.Deserialize<JoinMessage>(ref reader);
            case "QUIT":
                return JsonSerializer.Deserialize<QuitMessage>(ref reader);
            case "UPDATEUSER":
                return JsonSerializer.Deserialize<UpdateUserMessage>(ref reader);
            case "HISTORY":
                return JsonSerializer.Deserialize<HistoryMessage>(ref reader);
            case "ME":
                return JsonSerializer.Deserialize<MeMessage>(ref reader);
            case "NAMES":
                return JsonSerializer.Deserialize<NamesMessage>(ref reader);
            case "ERROR":
                return JsonSerializer.Deserialize<ErrorMessage>(ref reader);
            default:
                Console.WriteLine($"Unsupported message type `{messageType}`.");
                return null;
        }
    }
}
