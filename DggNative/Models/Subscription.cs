using System.Text.Json.Serialization;

namespace DggNative.Models;

public class Subscription
{
    [JsonPropertyName("tier")]
    public required int Tier { get; set; }
    
    [JsonPropertyName("source")]
    public required string Source { get; set; }
}