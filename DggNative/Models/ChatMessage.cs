namespace DggNative.Models;

public class ChatMessage(User user, string content)
{
    public User User = user;
    public string Content = content;
}