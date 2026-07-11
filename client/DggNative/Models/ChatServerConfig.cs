using System;

namespace DggNative.Models;

public enum ChatAuthMode
{
    /// <summary>destiny.gg-style auth: sid/rememberme cookies on the WebSocket handshake.</summary>
    Cookie,

    /// <summary>Standalone-server auth: a HELLO frame claiming a nick after connecting.</summary>
    Hello,
}

public record ChatServerConfig(Uri ServerUri, ChatAuthMode AuthMode, string? HelloNick = null)
{
    public static readonly Uri OfficialUri = new("wss://chat.destiny.gg/ws");

    private static readonly ChatServerConfig Official = new(OfficialUri, ChatAuthMode.Cookie);

    /// <summary>
    /// Computes the effective server config. The wsurl env var wins when set (developer
    /// escape hatch), otherwise the persisted settings. Invalid requested URLs are errors and
    /// never silently switch the application to another trust domain.
    /// </summary>
    public static ChatServerConfig Resolve(string? envUrl, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(envUrl))
        {
            if (!TryParseWsUri(envUrl, out var envUri))
                throw new ChatServerConfigurationException("The wsurl environment variable is not a valid ws:// or wss:// URL.");
            return new ChatServerConfig(envUri,
                EndpointTrust.IsApprovedOfficialChatEndpoint(envUri) ? ChatAuthMode.Cookie : ChatAuthMode.Hello);
        }

        if (settings.ServerKind == ServerKind.Custom)
        {
            if (!TryParseWsUri(settings.CustomServerUrl, out var customUri))
                throw new ChatServerConfigurationException("The configured custom server URL is invalid.");
            return new ChatServerConfig(customUri, ChatAuthMode.Hello);
        }

        return Official;
    }

    public static bool TryParseWsUri(string? input, out Uri uri)
    {
        if (Uri.TryCreate(input, UriKind.Absolute, out var parsed)
            && parsed.Scheme is "ws" or "wss")
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

}

public sealed class ChatServerConfigurationException(string message) : Exception(message);
