using System;

namespace DggNative.Models;

public static class WebSocketMessageFactory
{
    public static IWebSocketMessage? Create(string messageType, ReadOnlySpan<byte> jsonBytes)
    {
        throw new NotImplementedException();
    }
}