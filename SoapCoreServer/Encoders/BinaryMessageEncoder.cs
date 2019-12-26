using System;
using System.IO;
using System.ServiceModel.Channels;
using System.Xml;

namespace IsGa.Soap.Encoders
{
    internal class BinaryMessageEncoder : MessageEncoder
    {
        public BinaryMessageEncoder(MessageEncoder messageEncoder)
        {
            _innerEncoder = messageEncoder ?? throw new ArgumentNullException(nameof (messageEncoder));
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            throw new NotImplementedException();
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            var reader = XmlDictionaryReader.CreateBinaryReader(stream,
                                                                WcfBinary.WcfBinaryDictionary,
                                                                XmlDictionaryReaderQuotas.Max);

            return Message.CreateMessage(reader, maxSizeOfHeaders, MessageVersion);
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            throw new NotImplementedException();
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            var writer = XmlDictionaryWriter.CreateBinaryWriter(stream);

            message.WriteMessage(writer);
            writer.Flush();
        }

        public override string ContentType => "application/soap+msbin1";

        public override string MediaType => _innerEncoder.MediaType;

        public override MessageVersion MessageVersion => MessageVersion.Soap12WSAddressing10;

        private readonly MessageEncoder _innerEncoder;
    }
}
