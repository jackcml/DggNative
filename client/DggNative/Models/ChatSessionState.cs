namespace DggNative.Models;

public abstract record ChatSessionState;
public sealed record ChatSessionStopped : ChatSessionState;
public sealed record ChatSessionConnecting : ChatSessionState;
public sealed record ChatSessionGuest : ChatSessionState;
public sealed record ChatSessionAuthenticating : ChatSessionState;
public sealed record ChatSessionReady : ChatSessionState;
public sealed record ChatSessionRetrying(int MillisecondsUntilRetry) : ChatSessionState;
public sealed record ChatSessionRejected(string Code, string Message) : ChatSessionState;
