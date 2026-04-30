using System;

namespace DggNative.Models;

public sealed record ChatMessageTextSegment(string Text, Uri? Uri)
{
    public bool IsLink => Uri is not null;
}
