using System.Text.Json.Serialization;

namespace DggNative.Models;

public class Watching
{
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    [JsonPropertyName("platform")]
    public required string Platform { get; set; }
}