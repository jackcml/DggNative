namespace DggNative.Models
{
    /// <summary>
    /// Represents subscription information
    /// </summary>
    public class Subscription(int tier, string source)
    {
        public int tier { get; } = tier;
        public string source { get; } = source;
    }
}