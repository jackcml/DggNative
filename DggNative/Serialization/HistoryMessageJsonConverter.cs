using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DggNative.Models;

namespace DggNative.Serialization;

public class HistoryMessageJsonConverter : JsonConverter<HistoryMessage>
{
    public override HistoryMessage? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Expected StartArray for HistoryMessage payload.");
        }

        var historyMessage = new HistoryMessage();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return historyMessage;
            }

            if (reader.TokenType == JsonTokenType.String)
            {
                var rawMessage = reader.GetString();
                if (string.IsNullOrWhiteSpace(rawMessage)) continue;

                var firstSpaceIndex = rawMessage.IndexOf(' ');
                if (firstSpaceIndex == -1)
                {
                    // Handle cases where there might be no payload or malformed string
                    // For now, we can skip or log. Let's skip.
                    continue; 
                }

                var messageType = rawMessage.Substring(0, firstSpaceIndex);
                var jsonPayload = rawMessage.Substring(firstSpaceIndex + 1);
                
                // Convert payload string to bytes for the factory
                var jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);

                var message = WebSocketMessageFactory.Create(messageType, jsonBytes);
                if (message != null)
                {
                    historyMessage.Messages.Add(message);
                }
            }
        }

        throw new JsonException("Unexpected end of JSON while reading HistoryMessage.");
    }

    public override void Write(Utf8JsonWriter writer, HistoryMessage value, JsonSerializerOptions options)
    {
        throw new NotImplementedException("Serialization of HistoryMessage is not supported.");
    }
}
