using System.Text.Json;
using System.Text.RegularExpressions;

namespace DggNative.Models;

public static partial class OutboundFrames
{
    // Must match the standalone server's nick pattern (server/src/protocol.ts)
    [GeneratedRegex("^[A-Za-z0-9_][A-Za-z0-9_-]{0,31}$")]
    private static partial Regex NickPattern();

    public static bool IsValidNick(string nick) => NickPattern().IsMatch(nick);

    public static string Hello(string nick) => $"HELLO {JsonSerializer.Serialize(new { nick })}";

    public static string Msg(User user, string data) =>
        $"MSG {JsonSerializer.Serialize(new ChatMessage { User = user, Data = data })}";
}
