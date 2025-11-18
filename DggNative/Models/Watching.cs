using System.Text.Json.Serialization;

namespace DggNative.Models;

public class Watching
{
    [JsonPropertyName("platform")]
    public required string Platform { get; set; }

    [JsonPropertyName("id")]
    public required string Id { get; set; }
}