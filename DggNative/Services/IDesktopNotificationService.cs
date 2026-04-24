using System;
using DggNative.Models;

namespace DggNative.Services;

public interface IDesktopNotificationService
{
    event EventHandler? NotificationActivated;

    void ShowMentionNotification(ChatMessage message);
}
