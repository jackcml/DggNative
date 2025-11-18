using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DggNative.Serialization;

public class JsonFlattenConverter<T> : JsonConverter<T> where T : class
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var result = Activator.CreateInstance(typeToConvert)!;
        var props = typeToConvert.GetProperties();
        var flattenProps = props.Where(
            p => p.GetCustomAttribute<JsonFlattenAttribute>() != null).ToList();
        var normalProps = props.Except(flattenProps).ToList();

        var flattenedData = new Dictionary<string, JsonElement>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                break;

            var propName = reader.GetString()!;
            reader.Read();
            
            // Try to match with normal props first
            var matchedProp = normalProps.FirstOrDefault(
                p => GetJsonPropertyName(p).Equals(propName, StringComparison.OrdinalIgnoreCase));

            if (matchedProp != null)
            {
                var value = JsonSerializer.Deserialize(ref reader, matchedProp.PropertyType, options);
                matchedProp.SetValue(result, value);
            }
            else
            {
                var element = JsonElement.ParseValue(ref reader);
                flattenedData[propName] = element;
            }
        }

        if (flattenedData.Count <= 0) return (T)result;

        var doc = JsonDocument.Parse(
            JsonSerializer.SerializeToUtf8Bytes(flattenedData, options));
        foreach (var flatProp in flattenProps)
        {
            var flatValue = JsonSerializer.Deserialize(
                doc.RootElement.GetRawText(), flatProp.PropertyType, options);
            flatProp.SetValue(result, flatValue);
        }

        return (T)result;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        
        var props =  typeof(T).GetProperties();
        var  flattenProps = props.Where(
            p => p.GetCustomAttribute<JsonFlattenAttribute>() != null).ToList();
        var normalProps = props.Except(flattenProps).ToList();
        
        // Write normal props
        foreach (var prop in normalProps)
        {
            var propValue =  prop.GetValue(value);
            writer.WritePropertyName(GetJsonPropertyName(prop));
            JsonSerializer.Serialize(writer, propValue, prop.PropertyType, options);
        }
        
        // Write flattened props
        // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
        foreach (var flatProp in flattenProps)
        {
            var flatValue = flatProp.GetValue(value);
            var json = JsonSerializer.Serialize(flatValue, flatProp.PropertyType, options);
            var doc = JsonDocument.Parse(json);

            foreach (var element in doc.RootElement.EnumerateObject())
            {
                element.WriteTo(writer);
            }
        }
    }

    private static string GetJsonPropertyName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        return attr?.Name ?? prop.Name;
    }
}