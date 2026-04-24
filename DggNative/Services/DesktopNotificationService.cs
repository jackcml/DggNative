using System;
using DggNative.Models;
using Microsoft.Toolkit.Uwp.Notifications;

namespace DggNative.Services;

public sealed class DesktopNotificationService : IDesktopNotificationService
{
    private const int MaxBodyLength = 180;

    public void ShowMentionNotification(ChatMessage message)
    {
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
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to show desktop notification: {ex.Message}");
        }
    }

    private static string TrimNotificationBody(string body)
    {
        if (body.Length <= MaxBodyLength)
        {
            return body;
        }

        return body[..(MaxBodyLength - 3)] + "...";
    }
}
