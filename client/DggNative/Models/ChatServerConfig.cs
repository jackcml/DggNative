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
    /// escape hatch), otherwise the persisted settings; unparseable URLs fall back to official.
    /// </summary>
    public static ChatServerConfig Resolve(string? envUrl, AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(envUrl) && TryParseWsUri(envUrl, out var envUri))
        {
            return new ChatServerConfig(envUri, AuthModeForHost(envUri));
        }

        if (settings.ServerKind == ServerKind.Custom && TryParseWsUri(settings.CustomServerUrl, out var customUri))
        {
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

    private static ChatAuthMode AuthModeForHost(Uri uri)
    {
        var host = uri.Host;
        return host.Equals("destiny.gg", StringComparison.OrdinalIgnoreCase)
               || host.EndsWith(".destiny.gg", StringComparison.OrdinalIgnoreCase)
            ? ChatAuthMode.Cookie
            : ChatAuthMode.Hello;
    }
}
