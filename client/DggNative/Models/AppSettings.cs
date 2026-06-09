using System.Text.Json.Serialization;

namespace DggNative.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ServerKind
{
    Official,
    Custom,
}

public record AppSettings(
    ServerKind ServerKind = ServerKind.Official,
    string? CustomServerUrl = null,
    string? Nick = null);
