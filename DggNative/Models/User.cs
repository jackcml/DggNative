using System.Collections.Generic;

namespace DggNative.Models
{
    /// <summary>
    /// Represents user/session information
    /// </summary>
    public class User(int id, string nick, List<string> roles, List<string> features, string createdDate, Embed watching, Subscription subscription)
    {
        public int id { get; } = id;
        public string nick { get; } = nick;
        public List<string> roles { get; } = roles;
        public List<string> features { get; } = features;
        public string createdDate { get; } = createdDate;
        public Embed watching { get; } = watching;
        public Subscription subscription { get; } = subscription;
    }
}