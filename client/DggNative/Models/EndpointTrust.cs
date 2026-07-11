using System;

namespace DggNative.Models;

public static class EndpointTrust
{
    public static bool IsHostOrSubdomain(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase)
        || host.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase);

    public static bool IsApprovedOfficialChatEndpoint(Uri uri) =>
        uri.Scheme == Uri.UriSchemeWss
        && uri.Host.Equals("chat.destiny.gg", StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443
        && uri.AbsolutePath.Equals("/ws", StringComparison.Ordinal)
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}
