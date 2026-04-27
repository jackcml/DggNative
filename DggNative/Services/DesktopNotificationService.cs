using System;
using DggNative.Models;
#if WINDOWS
using Microsoft.Toolkit.Uwp.Notifications;
#endif

namespace DggNative.Services;

public sealed class DesktopNotificationService : IDesktopNotificationService
{
    private const int MaxBodyLength = 180;

    public event EventHandler? NotificationActivated;

    public void ShowMentionNotification(ChatMessage message)
    {
#if WINDOWS
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var title = $"{message.User.Nick} mentioned you";
        var body = TrimNotificationBody(message.Data);

        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show(toast =>
                {
                    toast.Group = "mentions";
                    toast.Tag = message.Timestamp > 0 ? message.Timestamp.ToString() : Guid.NewGuid().ToString();
                    toast.ExpirationTime = DateTimeOffset.Now.AddMinutes(10);
                    toast.Activated += (_, _) => NotificationActivated?.Invoke(this, EventArgs.Empty);
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to show desktop notification: {ex.Message}");
        }
#else
        Console.WriteLine($"[Notification] {message.User.Nick} mentioned you: {message.Data}");
#endif
    }

#if WINDOWS
    private static string TrimNotificationBody(string body)
    {
        if (body.Length <= MaxBodyLength)
        {
            return body;
        }

        return body[..(MaxBodyLength - 3)] + "...";
    }
#endif
}
