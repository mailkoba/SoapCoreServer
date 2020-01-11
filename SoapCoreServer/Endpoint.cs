using System;

namespace SoapCoreServer
{
    public class Endpoint
    {
        public Endpoint(string url, MessageType type)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentNullException(nameof(url));
            }

            if (!url.StartsWith("/"))
            {
                throw  new ArgumentException("Url must start wth '/'!");
            }

            Url = url;
            Type = type;
        }

        public string Url { get; }

        public MessageType Type { get; }
    }
}
