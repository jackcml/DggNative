using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DggNative.Models
{
    /// <summary>
    /// Represents a chat message with user information and content
    /// </summary>
    public class ChatMessage(User user, string data, long timestamp)
    {
        // User fields need to be flattened into this object on (de)serialization
        public User user = user;
        public long timestamp { get; } = timestamp;
        public string data { get; } = data;

        public string Serialize()
        {
            return JsonSerializer.Serialize(this, ChatMessageConverter.DefaultOptions);
        }

        public static ChatMessage Deserialize(string json)
        {
            return JsonSerializer.Deserialize<ChatMessage>(json, ChatMessageConverter.DefaultOptions);
        }
    }

    public class ChatMessageConverter : JsonConverter<ChatMessage>
    {
        public static readonly JsonSerializerOptions DefaultOptions = new JsonSerializerOptions
        {
            Converters = { new ChatMessageConverter() }
        };

        public override ChatMessage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument doc = JsonDocument.ParseValue(ref reader);
            JsonElement root = doc.RootElement;

            // Extract User properties from the flattened JSON
            int id = root.GetProperty("id").GetInt32();
            string nick = root.GetProperty("nick").GetString();

            var roles = new List<string>();
            if (root.TryGetProperty("roles", out JsonElement rolesElement))
            {
                foreach (JsonElement role in rolesElement.EnumerateArray())
                {
                    roles.Add(role.GetString());
                }
            }

            var features = new List<string>();
            if (root.TryGetProperty("features", out JsonElement featuresElement))
            {
                foreach (JsonElement feature in featuresElement.EnumerateArray())
                {
                    features.Add(feature.GetString());
                }
            }

            string createdDate = root.GetProperty("createdDate").GetString();

            Embed watching = null;
            if (root.TryGetProperty("watching", out JsonElement watchingElement))
            {
                if (watchingElement.ValueKind != JsonValueKind.Null)
                {
                    string platform = watchingElement.GetProperty("platform").GetString();
                    string embedId = watchingElement.GetProperty("id").GetString();
                    watching = new Embed(platform, embedId);
                }
            }

            Subscription subscription = null;
            if (root.TryGetProperty("subscription", out JsonElement subscriptionElement))
            {
                if (subscriptionElement.ValueKind != JsonValueKind.Null)
                {
                    var tier = subscriptionElement.GetProperty("tier").GetInt32();
                    var source = subscriptionElement.GetProperty("source").GetString();
                    subscription = new Subscription(tier, source);
                }
            }

            // Create User object
            User user = new(id, nick, roles, features, createdDate, watching, subscription);

            // Get ChatMessage-specific properties
            string data = root.GetProperty("data").GetString();
            long timestamp = root.GetProperty("timestamp").GetInt64();

            return new ChatMessage(user, data, timestamp);
        }

        public override void Write(Utf8JsonWriter writer, ChatMessage value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            // Write flattened User properties
            writer.WriteNumber("id", value.user.id);
            writer.WriteString("nick", value.user.nick);

            writer.WriteStartArray("roles");
            foreach (string role in value.user.roles)
            {
                writer.WriteStringValue(role);
            }
            writer.WriteEndArray();

            writer.WriteStartArray("features");
            foreach (string feature in value.user.features)
            {
                writer.WriteStringValue(feature);
            }
            writer.WriteEndArray();

            writer.WriteString("createdDate", value.user.createdDate);

            if (value.user.watching == null)
            {
                writer.WriteNull("watching");
            }
            else
            {
                writer.WriteStartObject("watching");
                writer.WriteString("platform", value.user.watching.platform);
                writer.WriteString("id", value.user.watching.id);
                writer.WriteEndObject();
            }

            if (value.user.subscription == null)
            {
                writer.WriteNull("subscription");
            }
            else
            {
                writer.WriteStartObject("subscription");
                writer.WriteNumber("tier", value.user.subscription.tier);
                writer.WriteString("source", value.user.subscription.source);
                writer.WriteEndObject();
            }

            // Write ChatMessage-specific properties
            writer.WriteNumber("timestamp", value.timestamp);
            writer.WriteString("data", value.data);

            writer.WriteEndObject();
        }
    }
}