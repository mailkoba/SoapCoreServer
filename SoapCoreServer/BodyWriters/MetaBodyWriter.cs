using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using System.Xml;
using SoapCoreServer.Descriptions;
using SoapCoreServer.Meta;

namespace SoapCoreServer.BodyWriters
{
    internal class MetaBodyWriter : BodyWriter
    {
        public MetaBodyWriter(ServiceDescription service, string baseUrl, Endpoint[] endpoints)
            : base(isBuffered: true)
        {
            _service = service;
            _baseUrl = baseUrl;
            _endpoints = endpoints;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            CreateSchemas();

            WritePolicies(writer);

            WriteTypes(writer);

            WriteMessages(writer);

            WritePortTypes(writer);

            WriteBindings(writer);

            WriteService(writer);
        }

        #region private

        private const string XmlnsXs = "http://www.w3.org/2001/XMLSchema";
        private const string TransportSchema = "http://schemas.xmlsoap.org/soap/http";

        private readonly ServiceDescription _service;
        private readonly string _baseUrl;
        private readonly Endpoint[] _endpoints;

        private WsdlDesc _wsdlDesc;
        private int _q;
        private int _basicBindingCounter = -1;
        private int _customBindingCounter = -1;

        private string BindingType => _service.ContractDescriptions.First().Name;

        private void CreateSchemas()
        {
            _wsdlDesc = new WsdlDesc();
            var contractsByNs = _service.ContractDescriptions
                                        .GroupBy(x => x.Namespace);

            foreach (var contractGroup in contractsByNs)
            {
                var schema = _wsdlDesc.GetSchema(contractGroup.Key);
                foreach (var contract in contractGroup)
                {
                    // перечень методов
                    foreach (var operation in contract.OperationDescriptions)
                    {
                        if (operation.IsStreamRequest)
                        {
                            schema.AddStreamMethod(operation);
                        }
                        else
                        {
                            if (!operation.IsEmptyRequest)
                            {
                                schema.AddMethod(operation.Request);
                            }

                            if (!operation.IsOneWay)
                            {
                                schema.AddMethod(operation.Response);
                            }
                        }
                    }
                }
            }
        }

        private void WriteComplexType(XmlDictionaryWriter writer, ElementDesc elem)
        {
            writer.WriteStartElement("xs:complexType");
            if (!elem.Root)
            {
                writer.WriteAttributeString("name", elem.Name);
            }

            var inherited = false;
            if (elem.Type != null)
            {
                var propType = Utils.GetFilteredPropertyType(elem.Type);
                inherited = !propType.isArray &&
                            !elem.IsStreamed &&
                            propType.type.BaseType != null &&
                            elem.Type.BaseType != typeof (object);

                // GenericType
                if (propType.type.IsGenericType)
                {
                    var attr = elem.Type.GetCustomAttribute<DataContractAttribute>();

                    writer.WriteStartElement("xs:annotation");
                    writer.WriteStartElement("xs:appinfo");

                    writer.WriteStartElement("GenericType");
                    writer.WriteAttributeString("xmlns", Utils.SerializationNs);
                    writer.WriteAttributeString("Name", attr.Name ?? propType.type.Name);
                    writer.WriteAttributeString("Namespace", Utils.GetNsByType(propType.type));
                    writer.WriteEndElement(); // GenericType

                    foreach (var argType in propType.type.GetGenericArguments())
                    {
                        writer.WriteStartElement("GenericParameter");
                        writer.WriteAttributeString("Name", Utils.GetTypeNameByContract(argType));
                        writer.WriteAttributeString("Namespace", Utils.GetNsByType(argType));
                        writer.WriteEndElement(); // GenericParameter
                    }

                    writer.WriteEndElement(); // xs:appinfo
                    writer.WriteEndElement(); // xs:annotation
                }

                if (inherited)
                {
                    var baseTypeNs = Utils.GetNsByType(propType.type.BaseType);

                    writer.WriteStartElement("xs:complexContent");
                    writer.WriteAttributeString("mixed", "false");
                    writer.WriteStartElement("xs:extension");

                    var baseTypeName = Utils.GetTypeNameByContract(propType.type.BaseType);
                    string xsTypename;
                    if (baseTypeNs == elem.Ns)
                    {
                        xsTypename = "tns:" + baseTypeName;
                    }
                    else
                    {
                        writer.WriteAttributeString($"xmlns:q{++_q}", baseTypeNs);
                        xsTypename = $"q{_q}:{baseTypeName}";
                    }

                    writer.WriteAttributeString("base", xsTypename);
                }
            }

            writer.WriteStartElement("xs:sequence");

            foreach (var childElem in elem.Children
                                          .Where(x => !x.NotWriteInComplexType))
            {
                WriteElement(writer, childElem);
            }

            writer.WriteEndElement(); // xs:sequence

            if (inherited)
            {
                writer.WriteEndElement(); // xs:complexContent
                writer.WriteEndElement(); // xs:extension
            }

            writer.WriteEndElement(); // xs:complexType
        }

        private void WriteElement(XmlDictionaryWriter writer, ElementDesc elem)
        {
            writer.WriteStartElement("xs:element");

            if (elem.Root)
            {
                writer.WriteAttributeString("name", elem.Name);
                WriteComplexType(writer, elem);
            }
            else
            {
                var typeInfo = elem.Type.GetTypeInfo();
                if (typeInfo.IsValueType)
                {
                    writer.WriteAttributeString("name", elem.Name);
                    if (typeInfo.IsEnum)
                    {
                        if (elem.Parent == null)
                        {
                            // top level element
                            if (elem.Nullable)
                            {
                                writer.WriteAttributeString("nillable", "true");
                            }
                        }
                        else
                        {
                            // inside complex type
                            if (!elem.Required)
                            {
                                writer.WriteAttributeString("minOccurs", "0");
                            }
                            if (elem.Nullable)
                            {
                                writer.WriteAttributeString("nillable", "true");
                            }
                            if (elem.Parent.Type.IsArray)
                            {
                                writer.WriteAttributeString("maxOccurs", "unbounded");
                            }
                        }

                        WriteElementType(writer, elem);
                    }
                    else
                    {
                        string xsTypename;
                        if (elem.Nullable)
                        {
                            xsTypename = Utils.ResolveType(elem.Type);
                            writer.WriteAttributeString("minOccurs", "0");
                            writer.WriteAttributeString("nillable", "true");
                        }
                        else
                        {
                            if (!elem.Required)
                            {
                                writer.WriteAttributeString("minOccurs", "0");
                            }

                            xsTypename = Utils.ResolveType(elem.Type);
                        }

                        writer.WriteAttributeString("type", xsTypename);
                    }
                }
                else
                {
                    if (elem.Type.Name == "String" || elem.Type.Name == "String&")
                    {
                        if (!elem.Required)
                        {
                            writer.WriteAttributeString("minOccurs", "0");
                        }
                        if (elem.Parent != null && elem.Parent.Type.IsArray)
                        {
                            writer.WriteAttributeString("maxOccurs", "unbounded");
                        }

                        writer.WriteAttributeString("name", elem.Name);
                        writer.WriteAttributeString("nillable", "true");
                        writer.WriteAttributeString("type", "xs:string");
                    }
                    else if (elem.Type.Name == "Byte[]")
                    {
                        writer.WriteAttributeString("name", elem.Name);
                        writer.WriteAttributeString("nillable", "true");
                        writer.WriteAttributeString("type", "xs:base64Binary");
                    }
                    else if (elem.Type.IsArray)
                    {
                        if (elem.Parent != null)
                        {
                            writer.WriteAttributeString("minOccurs", "0");
                        }

                        writer.WriteAttributeString("name", elem.Name);
                        writer.WriteAttributeString("nillable", "true");
                        WriteElementType(writer, elem);
                    }
                    else
                    {
                        if (!elem.Required && !elem.IsStreamed)
                        {
                            if (elem.Parent != null)
                            {
                                if (!elem.NotWriteInComplexType)
                                {
                                    writer.WriteAttributeString("minOccurs", "0");
                                }

                                if (elem.Parent.Type.IsArray)
                                {
                                    writer.WriteAttributeString("maxOccurs", "unbounded");
                                }
                            }

                            writer.WriteAttributeString("nillable", "true");
                        }

                        writer.WriteAttributeString("name", elem.Name);
                        WriteElementType(writer, elem);
                    }
                }
            }

            if (!elem.EmitDefaultValue)
            {
                writer.WriteStartElement("xs:annotation");
                writer.WriteStartElement("xs:appinfo");

                writer.WriteStartElement("DefaultValue");
                writer.WriteAttributeString("xmlns", Utils.SerializationNs);
                writer.WriteAttributeString("DefaultValue", "false");
                writer.WriteEndElement(); // EnumerationValue

                writer.WriteEndElement(); // xs:appinfo
                writer.WriteEndElement(); // xs:annotation
            }

            writer.WriteEndElement(); // xs:element
        }

        private void WriteElementType(XmlDictionaryWriter writer, ElementDesc elem)
        {
            string xsTypename;
            if (elem.Parent == null || elem.Parent.Ns == elem.Ns)
            {
                xsTypename = $"tns:{elem.TypeName}";
            }
            else
            {
                writer.WriteAttributeString($"xmlns:q{++_q}", elem.Ns);
                xsTypename = $"q{_q}:{elem.TypeName}";
            }

            writer.WriteAttributeString("type", xsTypename);
        }

        private void WriteTypes(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:types");

            var mainNs = _service.ContractDescriptions.First().Namespace;

            foreach (var ns in _wsdlDesc.AllNs)
            {
                if (ns == Utils.StreamNs)
                {
                    Utils.WriteStreamSchema(writer);
                    continue;
                }

                var schema = _wsdlDesc.GetSchema(ns);

                writer.WriteStartElement("xs:schema");
                writer.WriteAttributeString("xmlns:xs", XmlnsXs);

                if (schema.HasSerializationTypes)
                {
                    writer.WriteAttributeString("xmlns:ser", Utils.SerializationNs);
                }

                writer.WriteAttributeString("elementFormDefault", "qualified");
                writer.WriteAttributeString("targetNamespace", ns);

                if (ns != mainNs)
                {
                    writer.WriteAttributeString("xmlns:tns", ns);
                }

                var importNs = schema.ImportNs.ToList();
                if (!importNs.Contains(Utils.SerializationNs))
                {
                    importNs.Add(Utils.SerializationNs);
                }

                if (schema.Elements.Any(x => x.Type == typeof (Stream)))
                {
                    importNs.Add(Utils.StreamNs);
                }

                foreach (var import in importNs)
                {
                    writer.WriteStartElement("xs:import");
                    writer.WriteAttributeString("namespace", import);
                    writer.WriteEndElement();
                }

                foreach (var elem in schema.Elements)
                {
                    WriteElement(writer, elem);
                }

                foreach (var complexType in schema.ComplexTypes)
                {
                    WriteComplexType(writer, complexType);
                }

                WriteEnumTypes(writer, schema);

                writer.WriteEndElement(); // xs:schema
            }

            Utils.WriteSerializationSchema(writer);

            writer.WriteEndElement(); // wsdl:types
        }

        private void WriteEnumTypes(XmlDictionaryWriter writer, SchemaDesc schema)
        {
            foreach (var toBuild in schema.Enums)
            {
                var name = Utils.GetTypeNameByContract(toBuild);

                writer.WriteStartElement("xs:simpleType");
                writer.WriteAttributeString("name", name);
                writer.WriteStartElement("xs:restriction ");
                writer.WriteAttributeString("base", "xs:string");

                var iEnum = 0;
                foreach (var value in Enum.GetValues(toBuild))
                {
                    writer.WriteStartElement("xs:enumeration ");
                    writer.WriteAttributeString("value", value.ToString());

                    if ((int) value != iEnum)
                    {
                        writer.WriteStartElement("xs:annotation");
                        writer.WriteStartElement("xs:appinfo");

                        writer.WriteStartElement("EnumerationValue");
                        writer.WriteAttributeString("xmlns", Utils.SerializationNs);
                        writer.WriteValue((int) value);
                        writer.WriteEndElement(); // EnumerationValue

                        writer.WriteEndElement(); // xs:appinfo
                        writer.WriteEndElement(); // xs:annotation
                    }

                    writer.WriteEndElement(); // xs:enumeration
                    iEnum++;
                }

                writer.WriteEndElement(); // xs:restriction
                writer.WriteEndElement(); // xs:simpleType
            }
        }

        private void WriteMessages(XmlDictionaryWriter writer)
        {
            var added = new HashSet<string>();
            foreach (var operation in _service.OperationDescriptions)
            {
                // Input.
                var requestMessageName = operation.IsStreamRequest
                                             ? $"{operation.ContractDescription.Name}_{operation.Name}_InputMessage"
                                             : operation.Request.MessageName;
                if (!added.Contains(requestMessageName))
                {
                    writer.WriteStartElement("wsdl:message");
                    writer.WriteAttributeString("name", requestMessageName);

                    if (!operation.IsEmptyRequest)
                    {
                        writer.WriteStartElement("wsdl:part");
                        writer.WriteAttributeString("name", "parameters");
                        writer.WriteAttributeString("element",
                                                    "tns:" + (operation.IsStreamRequest
                                                                  ? operation.Name
                                                                  : operation.Request.MessageName));
                        writer.WriteEndElement(); // wsdl:part
                    }

                    writer.WriteEndElement(); // wsdl:message

                    added.Add(requestMessageName);
                }

                // Headers
                if (operation.Request.Headers.Length > 0)
                {
                    var headerName = $"{operation.Request.MessageName}_Headers";
                    if (!added.Contains(headerName))
                    {
                        writer.WriteStartElement("wsdl:message");
                        writer.WriteAttributeString("name", headerName);

                        foreach (var header in operation.Request.Headers)
                        {
                            writer.WriteStartElement("wsdl:part");
                            writer.WriteAttributeString("name", header.Name);
                            writer.WriteAttributeString("element", "tns:" + header.Name);
                            writer.WriteEndElement(); // wsdl:part
                        }

                        writer.WriteEndElement(); // wsdl:message

                        added.Add(headerName);
                    }
                }

                // Output.
                if (!operation.IsOneWay)
                {
                    var responseMessageName = operation.IsStreamRequest
                                                  ? $"{operation.ContractDescription.Name}_{operation.Name}_OutputMessage"
                                                  : operation.Response.MessageName;
                    if (!added.Contains(responseMessageName))
                    {
                        writer.WriteStartElement("wsdl:message");
                        writer.WriteAttributeString("name", responseMessageName);
                        writer.WriteStartElement("wsdl:part");
                        writer.WriteAttributeString("name", "parameters");
                        writer.WriteAttributeString("element", "tns:" + operation.Response.MessageName);
                        writer.WriteEndElement(); // wsdl:part
                        writer.WriteEndElement(); // wsdl:message

                        added.Add(operation.Response.MessageName);
                    }
                }
            }
        }

        private void WritePortTypes(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("wsdl:portType");
            writer.WriteAttributeString("name", BindingType);

            foreach (var operation in _service.OperationDescriptions)
            {
                writer.WriteStartElement("wsdl:operation");
                writer.WriteAttributeString("name", operation.Name);

                writer.WriteStartElement("wsdl:input");
                writer.WriteAttributeString("wsaw:Action", operation.SoapAction);

                if (!(operation.IsEmptyRequest || operation.IsStreamRequest))
                {
                    writer.WriteAttributeString("name", operation.Request.MessageName);
                }

                var requestMessageName = operation.IsStreamRequest
                                             ? $"{operation.ContractDescription.Name}_{operation.Name}_InputMessage"
                                             : operation.Request.MessageName;

                writer.WriteAttributeString("message", $"tns:{requestMessageName}");
                writer.WriteEndElement(); // wsdl:input

                if (!operation.IsOneWay)
                {
                    writer.WriteStartElement("wsdl:output");
                    writer.WriteAttributeString("wsaw:Action", operation.ReplyAction);

                    if (!operation.IsStreamRequest)
                    {
                        writer.WriteAttributeString("name", operation.Response.MessageName);
                    }

                    var responseMessageName = operation.IsStreamRequest
                                                  ? $"{operation.ContractDescription.Name}_{operation.Name}_OutputMessage"
                                                  : operation.Response.MessageName;

                    writer.WriteAttributeString("message", $"tns:{responseMessageName}");
                    writer.WriteEndElement(); // wsdl:output
                }

                writer.WriteEndElement(); // wsdl:operation
            }

            writer.WriteEndElement(); // wsdl:portType
        }

        private void WriteBindings(XmlDictionaryWriter writer)
        {
            ResetCounters();

            foreach (var endpoint in _endpoints)
            {
                var portInfo = GetPortInfo(endpoint);

                writer.WriteStartElement("wsdl:binding");
                writer.WriteAttributeString("name", portInfo.portName);
                writer.WriteAttributeString("type", $"tns:{BindingType}");

                if (!endpoint.Type.IsText())
                {
                    writer.WriteStartElement("wsp:PolicyReference");
                    writer.WriteAttributeString("URI", $"#{portInfo.portName}_policy");
                    writer.WriteEndElement(); // wsp:PolicyReference
                }

                writer.WriteStartElement($"{portInfo.soapPrefix}:binding");
                writer.WriteAttributeString("transport", TransportSchema);
                writer.WriteEndElement(); // soap:binding

                foreach (var operation in _service.OperationDescriptions)
                {
                    writer.WriteStartElement("wsdl:operation");
                    writer.WriteAttributeString("name", operation.Name);

                    writer.WriteStartElement($"{portInfo.soapPrefix}:operation");
                    writer.WriteAttributeString("soapAction", operation.SoapAction);
                    writer.WriteAttributeString("style", "document");
                    writer.WriteEndElement(); // soap:operation

                    writer.WriteStartElement("wsdl:input");
                    if (!(operation.IsEmptyRequest || operation.IsStreamRequest))
                    {
                        writer.WriteAttributeString("name", operation.Request.MessageName);
                    }

                    foreach (var header in operation.Request.Headers)
                    {
                        writer.WriteStartElement($"{portInfo.soapPrefix}:header");
                        writer.WriteAttributeString("message", $"tns:{operation.Request.MessageName}_Headers");
                        writer.WriteAttributeString("part", header.Name);
                        writer.WriteAttributeString("use", "literal");

                        writer.WriteEndElement(); // soap:header
                    }

                    writer.WriteStartElement($"{portInfo.soapPrefix}:body");
                    writer.WriteAttributeString("use", "literal");
                    writer.WriteEndElement(); // soap:body
                    writer.WriteEndElement(); // wsdl:input

                    if (!operation.IsOneWay)
                    {
                        writer.WriteStartElement("wsdl:output");
                        if (!operation.IsStreamRequest)
                        {
                            writer.WriteAttributeString("name", operation.Response.MessageName);
                        }

                        writer.WriteStartElement($"{portInfo.soapPrefix}:body");
                        writer.WriteAttributeString("use", "literal");
                        writer.WriteEndElement(); // soap:body
                        writer.WriteEndElement(); // wsdl:output
                    }

                    writer.WriteEndElement(); // wsdl:operation
                }

                writer.WriteEndElement(); // wsdl:binding
            }
        }

        private void WriteService(XmlDictionaryWriter writer)
        {
            ResetCounters();

            writer.WriteStartElement("wsdl:service");
            writer.WriteAttributeString("name", _service.ServiceType.Name);

            foreach (var endpoint in _endpoints)
            {
                var portInfo = GetPortInfo(endpoint);

                writer.WriteStartElement("wsdl:port");
                writer.WriteAttributeString("name", portInfo.portName);
                writer.WriteAttributeString("binding", $"tns:{portInfo.portName}");

                if (endpoint.Type.IsText())
                {
                    writer.WriteStartElement("soap:address");

                    writer.WriteAttributeString("location", $"{_baseUrl}{endpoint.Url}");
                    writer.WriteEndElement(); // soap:address
                }
                else
                {
                    writer.WriteStartElement("soap12:address");

                    writer.WriteAttributeString("location", $"{_baseUrl}{endpoint.Url}");
                    writer.WriteEndElement(); // soap:address

                    writer.WriteStartElement("wsa10:EndpointReference");
                    writer.WriteStartElement("wsa10:Address");
                    writer.WriteString($"{_baseUrl}{endpoint.Url}");
                    writer.WriteEndElement(); // wsa10:Address
                    writer.WriteEndElement(); // wsa10:EndpointReference
                }

                writer.WriteEndElement(); // wsdl:port
            }
        }

        private void WritePolicies(XmlDictionaryWriter writer)
        {
            ResetCounters();

            foreach (var endpoint in _endpoints.Where(x => !x.Type.IsText()))
            {
                var portInfo = GetPortInfo(endpoint);

                writer.WriteStartElement("wsp:Policy");
                writer.WriteAttributeString("wsu:Id", $"{portInfo.portName}_policy");

                writer.WriteStartElement("wsp:ExactlyOne");
                writer.WriteStartElement("wsp:All");

                writer.WriteStartElement("msb:BinaryEncoding");
                writer.WriteAttributeString("xmlns:msb",
                                            "http://schemas.microsoft.com/ws/06/2004/mspolicy/netbinary1");
                writer.WriteStartElement("wsaw:UsingAddressing");
                writer.WriteEndElement(); // wsaw:UsingAddressing
                writer.WriteEndElement(); // msb:BinaryEncoding

                writer.WriteEndElement(); // wsp:All
                writer.WriteEndElement(); // wsp:ExactlyOne
                writer.WriteEndElement(); // wsp:Policy
            }
        }

        private void ResetCounters()
        {
            _basicBindingCounter = -1;
            _customBindingCounter = -1;
        }

        private (string soapPrefix, string portName) GetPortInfo(Endpoint endpoint)
        {
            var serviceTypeName = _service.ContractDescriptions.First().Name;
            if (endpoint.Type.IsText())
            {
                _basicBindingCounter++;
                return (soapPrefix: "soap",
                           portName:
                           $"BasicHttpBinding_{serviceTypeName}{(_basicBindingCounter > 0 ? _basicBindingCounter.ToString() : string.Empty)}");
            }
            else
            {
                _customBindingCounter++;
                return (soapPrefix: "soap12",
                           portName:
                           $"CustomBinding_{serviceTypeName}{(_customBindingCounter > 0 ? _customBindingCounter.ToString() : string.Empty)}");
            }
        }

        #endregion private
    }
}
