namespace DggNative.Models;

public record AuthCookies(string? Sid, string? RememberMe)
{
    public bool HasCredentials => !string.IsNullOrEmpty(Sid) || !string.IsNullOrEmpty(RememberMe);
}
