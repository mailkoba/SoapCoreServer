using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;
using System.Xml.Serialization;
using SoapCoreServer.Descriptions;

namespace SoapCoreServer.BodyWriters
{
    internal class WrappedBodyWriter : BodyWriter
    {
        public WrappedBodyWriter(OperationDataDescription operation,
                                 object body)
            : base(isBuffered: true)
        {
            _operation = operation;
            _body = body;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter xmlWriter)
        {
            if (_operation.IsWrapped)
            {
                xmlWriter.WriteStartElement(_operation.MessageName,
                                            _operation.Operation.ContractDescription.Namespace);
            }

            var props = _body.GetType()
                             .GetFieldsAndProperties();

            foreach (var prop in props)
            {
                var value = prop.GetValue(_body);
                Write(prop, xmlWriter, value);
            }

            if (_operation.IsWrapped)
            {
                xmlWriter.WriteEndElement();
            }
        }

        private readonly OperationDataDescription _operation;
        private readonly object _body;

        private void Write(MemberInfo prop, XmlDictionaryWriter xmlWriter, object value)
        {
            switch (_operation.Operation.ContractDescription.ServiceDescription.SoapSerializer)
            {
                case SoapSerializerType.DataContractSerializer:
                    var dataContractSerializer = new DataContractSerializer(prop.GetMemberType(),
                                                                            prop.Name,
                                                                            _operation.Operation.ContractDescription
                                                                                .Namespace);

                    dataContractSerializer.WriteObject(xmlWriter, value);
                    break;
                case SoapSerializerType.XmlSerializer:
                    var xmlSerializer = new XmlSerializer(prop.GetMemberType(),
                                                          overrides: null,
                                                          extraTypes: Array.Empty<Type>(),
                                                          new XmlRootAttribute(prop.Name),
                                                          _operation.Operation.ContractDescription.Namespace);
                    xmlSerializer.Serialize(xmlWriter, value);
                    break;
                default:
                    throw new Exception(
                        $"Unknown SoapSerializerType '{_operation.Operation.ContractDescription.ServiceDescription.SoapSerializer}'!");
            }
        }
    }
}
