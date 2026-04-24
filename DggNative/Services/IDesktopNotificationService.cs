using DggNative.Models;

namespace DggNative.Services;

public interface IDesktopNotificationService
{
    void ShowMentionNotification(ChatMessage message);
}
