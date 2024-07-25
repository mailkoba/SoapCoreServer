using System;
using System.Security.Authentication;

namespace SoapCoreServer.Client
{
    public class SoapClientOptions
    {
        private SoapClientOptions()
        {
        }

        public MessageType MessageType { get; private set; }

        public string ServiceUrl { get; private set; }

        public bool DoNotCheckCertificates { get; private set; }

        public SslProtocols SslProtocols { get; private set; } = SslProtocols.Tls12 | SslProtocols.Tls13;

        public SoapSerializerType SerializerType { get; private set; }

        public static SoapClientOptions Create(string serviceUrl,
                                               MessageType messageType = MessageType.Text,
                                               SoapSerializerType serializerType = SoapSerializerType.XmlSerializer,
                                               bool doNotCheckCertificates = false,
                                               SslProtocols? sslProtocols = null)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                throw new ArgumentNullException(nameof (serviceUrl));
            }

            var options = new SoapClientOptions
            {
                ServiceUrl = serviceUrl,
                MessageType = messageType,
                DoNotCheckCertificates = doNotCheckCertificates,
                SerializerType = serializerType
            };

            if (sslProtocols.HasValue)
            {
                options.SslProtocols = sslProtocols.Value;
            }

            return options;
        }
    }
}
