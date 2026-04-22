namespace DggNative.Rendering;

public readonly record struct ChatInlinePart(string Text, string? ImageUrl)
{
    public bool IsEmote => ImageUrl is not null;

    public static ChatInlinePart TextPart(string text) => new(text, null);

    public static ChatInlinePart EmotePart(string name, string imageUrl) => new(name, imageUrl);
}
