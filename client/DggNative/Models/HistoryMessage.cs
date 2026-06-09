using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using DggNative.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(HistoryMessageJsonConverter))]
public class HistoryMessage : IWebSocketMessage
{
    public List<IWebSocketMessage> Messages { get; } = new();
}

