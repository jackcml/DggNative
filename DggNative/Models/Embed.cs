namespace DggNative.Models
{
    /// <summary>
    /// Represents embed information
    /// </summary>
    public class Embed
    {
        public string platform { get; }
        public string id { get; }

        public Embed(string platform, string id)
        {
            this.platform = platform;
            this.id = id;
        }
    }
}