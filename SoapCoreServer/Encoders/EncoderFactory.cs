using System;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace SoapCoreServer.Encoders
{
    internal class EncoderFactory
    {
        public static MessageEncoder Create(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Text:
                    return DefaultEncoder;
                case MessageType.Binary:
                    return new BinaryMessageEncoder(DefaultEncoder);
                case MessageType.BinaryGZip:
                    return new GZipMessageEncoder(new BinaryMessageEncoder(DefaultEncoder));
                case MessageType.BinaryDeflate:
                    return new DeflateMessageEncoder(new BinaryMessageEncoder(DefaultEncoder));
                case MessageType.StreamText:
                    return DefaultEncoder;
                case MessageType.StreamBinary:
                    return new BinaryMessageEncoder(DefaultEncoder);
                case MessageType.StreamBinaryGZip:
                    return new GZipMessageEncoder(new BinaryMessageEncoder(DefaultEncoder));
                case MessageType.StreamBinaryDeflate:
                    return new DeflateMessageEncoder(new BinaryMessageEncoder(DefaultEncoder));
            }
            throw new Exception("Unsupported message type!");
        }

        public static MessageEncoder DefaultEncoder
        {
            get
            {
                if (_encoder == null)
                {
                    lock (LockObj)
                    {
                        if (_encoder == null)
                        {
                            var element = new BasicHttpBinding().CreateBindingElements()
                                                                .Find<MessageEncodingBindingElement>();
                            var factory = element.CreateMessageEncoderFactory();
                            _encoder = factory.Encoder;
                        }
                    }
                }

                return _encoder;
            }
        }

        private static MessageEncoder _encoder;
        private static readonly object LockObj = new object();
    }
}
