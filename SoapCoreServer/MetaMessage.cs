using System.Linq;
using System.ServiceModel.Channels;
using System.Xml;
using IsGa.Soap.Descriptions;

namespace IsGa.Soap
{
    internal class MetaMessage : Message
    {
        public MetaMessage(Message message, ServiceDescription service)
        {
            _message = message;
            _service = service;
        }

        /// <summary>
        /// override to replace s:Envelope
        /// </summary>
        /// <param name="writer"></param>
        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:definitions");
            writer.WriteAttributeString("name", _service.ServiceType.Name);
            writer.WriteAttributeString("targetNamespace", _service.ContractDescriptions.First().Namespace);
            writer.WriteAttributeString("xmlns:wsdl", "http://schemas.xmlsoap.org/wsdl/");
            writer.WriteAttributeString("xmlns:wsap", "http://schemas.xmlsoap.org/ws/2004/08/addressing/policy");
            writer.WriteAttributeString("xmlns:wsa10", "http://www.w3.org/2005/08/addressing");
            writer.WriteAttributeString("xmlns:tns", _service.ContractDescriptions.First().Namespace);
            writer.WriteAttributeString("xmlns:msc", "http://schemas.microsoft.com/ws/2005/12/wsdl/contract");
            writer.WriteAttributeString("xmlns:soapenc", "http://schemas.xmlsoap.org/soap/encoding/");
            writer.WriteAttributeString("xmlns:wsx", "http://schemas.xmlsoap.org/ws/2004/09/mex");
            writer.WriteAttributeString("xmlns:soap", "http://schemas.xmlsoap.org/wsdl/soap/");
            writer.WriteAttributeString("xmlns:wsam", "http://www.w3.org/2007/05/addressing/metadata");
            writer.WriteAttributeString("xmlns:wsa", "http://schemas.xmlsoap.org/ws/2004/08/addressing");
            writer.WriteAttributeString("xmlns:wsp", "http://schemas.xmlsoap.org/ws/2004/09/policy");
            writer.WriteAttributeString("xmlns:wsaw", "http://www.w3.org/2006/05/addressing/wsdl");
            writer.WriteAttributeString("xmlns:soap12", "http://schemas.xmlsoap.org/wsdl/soap12/");
            writer.WriteAttributeString("xmlns:wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            writer.WriteAttributeString("xmlns:xsd", "http://www.w3.org/2001/XMLSchema");
        }

        /// <summary>
        /// override to replace s:Body
        /// </summary>
        /// <param name="writer"></param>
        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            //writer.WriteStartElement("wsdl:types");
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            _message.WriteBodyContents(writer);
        }

        public override MessageHeaders Headers => _message.Headers;

        public override MessageProperties Properties => _message.Properties;

        public override MessageVersion Version => _message.Version;

        private readonly Message _message;
        private readonly ServiceDescription _service;
    }
}
