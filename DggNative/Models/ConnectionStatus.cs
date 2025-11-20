namespace DggNative.Models;

public abstract record ConnectionStatus;
public record ConnectionStatusConnected : ConnectionStatus;
public record ConnectionStatusDisconnected : ConnectionStatus;
public record ConnectionStatusConnecting : ConnectionStatus;
public record ConnectionStatusRetrying(int MillisecondsUntilRetry) : ConnectionStatus;
