using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml;

namespace IsGa.Soap.BodyWriters
{
    public class FaultBodyWriter : BodyWriter
    {
        public FaultBodyWriter(FaultMessage fault, EnvelopeVersion envelopeVersion)
            : base(true)
        {
            _fault = fault;
            _envelopeVersion = envelopeVersion;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            _fault.WriteTo(writer, _envelopeVersion);
        }

        private readonly FaultMessage _fault;
        private readonly EnvelopeVersion _envelopeVersion;
    }
}
