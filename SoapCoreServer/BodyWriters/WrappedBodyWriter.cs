using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;

namespace IsGa.Soap.BodyWriters
{
    internal class WrappedBodyWriter : BodyWriter
    {
        public WrappedBodyWriter(string serviceNamespace,
                                 string bodyName,
                                 object body)
            : base(isBuffered: true)
        {
            _serviceNamespace = serviceNamespace;
            _bodyName = bodyName;
            _body = body;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter xmlWriter)
        {
            xmlWriter.WriteStartElement(_bodyName, _serviceNamespace);

            var props = _body.GetType()
                             .GetFieldsAndProperties();

            foreach (var prop in props)
            {
                var value = prop.GetValue(_body);

                var serializer = new DataContractSerializer(prop.GetMemberType(), prop.Name, _serviceNamespace);
                serializer.WriteObject(xmlWriter, value);
            }

            xmlWriter.WriteEndElement();
        }

        private readonly string _serviceNamespace;
        private readonly string _bodyName;
        private readonly object _body;
    }
}
