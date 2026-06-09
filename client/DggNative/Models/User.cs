using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DggNative.Models;

public class User
{
    [JsonPropertyName("id")]
    public required int Id { get; set; }

    [JsonPropertyName("nick")]
    public required string Nick { get; set; }

    [JsonPropertyName("roles")]
    public required List<string> Roles { get; set; }
    
    [JsonPropertyName("features")]
    public required List<string> Features { get; set; }

    [JsonPropertyName("createdDate")]
    public required string CreatedDate { get; set; }

    [JsonPropertyName("watching")]
    public Watching? Watching { get; set; }

    [JsonPropertyName("subscription")]
    public Subscription? Subscription { get; set; }
}