using System;
using System.IO;
using System.IO.Compression;
using System.ServiceModel.Channels;

namespace SoapCoreServer.Encoders
{
    internal class DeflateMessageEncoder : MessageEncoder
    {
        public DeflateMessageEncoder(MessageEncoder messageEncoder)
        {
            _innerEncoder = messageEncoder ?? throw new ArgumentNullException(nameof(messageEncoder));
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            throw new NotImplementedException();
        }

        public override Message ReadMessage(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            var dfStream = new DeflateStream(stream, CompressionMode.Decompress, false);
            return _innerEncoder.ReadMessage(dfStream, maxSizeOfHeaders);
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            throw new NotImplementedException();
        }

        public override void WriteMessage(Message message, Stream stream)
        {
            using (var dfStream = new DeflateStream(stream, CompressionMode.Compress, true))
            {
                _innerEncoder.WriteMessage(message, dfStream);
            }
            stream.Flush();
        }

        public override string ContentType => "application/soap+msbin1+deflate";

        public override string MediaType => _innerEncoder.MediaType;

        public override MessageVersion MessageVersion => MessageVersion.Soap12WSAddressing10;

        private readonly MessageEncoder _innerEncoder;
    }
}
