using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Xml;

namespace SoapCoreServer
{
    internal static class Utils
    {
        public static (string name, bool required, int order, bool nullable, bool emitDefaultValue)? GetDataMemberInfo(MemberInfo prop)
        {
            var attr = prop.GetCustomAttribute<DataMemberAttribute>();
            if (attr == null) return null;
            return (name: attr.Name ?? prop.Name,
                    required: attr.IsRequired,
                    order: attr.Order,
                    nullable: Nullable.GetUnderlyingType(prop.GetMemberType()) != null,
                    emitDefaultValue: attr.EmitDefaultValue);
        }

        public static (Type type, bool isArray) GetFilteredPropertyType(Type type)
        {
            if (type == typeof (Stream))
            {
                return (type, isArray: false);
            }

            type = GetUnderlyingType(type);
            if (type == typeof (string))
            {
                return (type, isArray: false);
            }

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return (type: elementType, isArray: true);
            }

            if (typeof (IEnumerable).IsAssignableFrom(type))
            {
                // Recursively look through the base class to find the Generic Type of the Enumerable
                var baseType = type;
                var baseTypeInfo = type.GetTypeInfo();
                while (!baseTypeInfo.IsGenericType && baseTypeInfo.BaseType != null)
                {
                    baseType = baseTypeInfo.BaseType;
                    baseTypeInfo = baseType.GetTypeInfo();
                }

                var generic = baseType.GetTypeInfo().GetGenericArguments().DefaultIfEmpty(typeof (object))
                                      .FirstOrDefault();
                return (type: generic, isArray: true);
            }

            return (type, isArray: false);
        }

        public static Type GetUnderlyingType(Type type)
        {
            return Nullable.GetUnderlyingType(type) ?? type;
        }

        public static string ResolveType(Type type)
        {
            var typeName = type.IsEnum ? type.GetEnumUnderlyingType().Name : type.Name;

            switch (typeName)
            {
                case "Boolean":
                    return "xs:boolean";
                case "Byte":
                    return "xs:unsignedByte";
                case "Int16":
                    return "xs:short";
                case "Int32":
                    return "xs:int";
                case "Int64":
                    return "xs:long";
                case "SByte":
                    return "xs:byte";
                case "UInt16":
                    return "xs:unsignedShort";
                case "UInt32":
                    return "xs:unsignedInt";
                case "UInt64":
                    return "xs:unsignedLong";
                case "Decimal":
                    return "xs:decimal";
                case "Double":
                    return "xs:double";
                case "Single":
                    return "xs:float";
                case "DateTime":
                    return "xs:dateTime";
                case "Guid":
                    return "ser:guid";
            }

            throw new ArgumentException($".NET type {typeName} cannot be resolved into XML schema type!");
        }

        public static string GetTypeNameByContract(Type type)
        {
            var filteredType = GetFilteredPropertyType(type);
            var attr = filteredType.type.GetCustomAttribute<DataContractAttribute>();

            var name = attr?.Name;
            if (name != null && type.IsGenericType)
            {
                var generic = type.GetTypeInfo()
                                  .GetGenericArguments()
                                  .Select(GetTypeNameByContract)
                                  .ToArray();
                name = string.Format(name, generic);
            }

            return name ?? (filteredType.type == typeof (string) ? "string" : filteredType.type.Name);
        }

        public static string GetNsByType(Type type)
        {
            if (type == typeof (Stream)) return StreamNs;

            var filteredType = GetFilteredPropertyType(type);
            var attr = filteredType.type.GetCustomAttribute<DataContractAttribute>();

            return attr?.Namespace ?? (type == typeof (string[])
                                           ? SerializationArraysNs
                                           : $"{DataContractNs}/{filteredType.type.Namespace}");
        }

        public static void ValidateBasePath(string basePath)
        {
            if (string.IsNullOrWhiteSpace(basePath))
            {
                throw new ArgumentException("basePath is empty!");
            }

            if (!basePath.StartsWith("/"))
            {
                throw new ArgumentException("Url must start wth '/'!");
            }

            if (basePath.EndsWith(("/")))
            {
                throw new ArgumentException("Url must not ends wth '/'!");
            }
        }

        public const string DataContractNs = "http://schemas.datacontract.org/2004/07";
        public const string SerializationNs = "http://schemas.microsoft.com/2003/10/Serialization/";
        public const string SerializationArraysNs = "http://schemas.microsoft.com/2003/10/Serialization/Arrays";
        public const string StreamNs = "http://schemas.microsoft.com/Message";

        public static void WriteSerializationSchema(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("xs", "schema", SoapNamespaces.Xsd);
            writer.WriteXmlnsAttribute("xs", SoapNamespaces.Xsd);
            writer.WriteXmlnsAttribute("tns", SerializationNs);
            writer.WriteAttributeString("elementFormDefault", "qualified");
            writer.WriteAttributeString("targetNamespace", SerializationNs);

            foreach (var elem in SerElements)
            {
                writer.WriteStartElement("xs", "element", SoapNamespaces.Xsd);
                writer.WriteAttributeString("name", elem);
                writer.WriteAttributeString("nillable", "true");
                writer.WriteAttributeString("type", $"xs:{elem}");
                writer.WriteEndElement(); // xs:element
            }

            // 

            writer.WriteStartElement("xs", "element", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "char");
            writer.WriteAttributeString("nillable", "true");
            writer.WriteAttributeString("type", "tns:char");
            writer.WriteEndElement(); // xs:element

            writer.WriteStartElement("xs", "simpleType", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "char");
            writer.WriteStartElement("xs", "restriction", SoapNamespaces.Xsd);
            writer.WriteAttributeString("base", "xs:int");
            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            //

            writer.WriteStartElement("xs", "element", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "duration");
            writer.WriteAttributeString("nillable", "true");
            writer.WriteAttributeString("type", "tns:duration");
            writer.WriteEndElement(); // xs:element

            writer.WriteStartElement("xs", "simpleType", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "duration");

            writer.WriteStartElement("xs", "restriction", SoapNamespaces.Xsd);
            writer.WriteAttributeString("base", "xs:duration");

            writer.WriteStartElement("xs", "pattern", SoapNamespaces.Xsd);
            writer.WriteAttributeString("value", @"\-?P(\d*D)?(T(\d*H)?(\d*M)?(\d*(\.\d*)?S)?)?");
            writer.WriteEndElement(); // xs:pattern

            writer.WriteStartElement("xs", "minInclusive", SoapNamespaces.Xsd);
            writer.WriteAttributeString("value", "-P10675199DT2H48M5.4775808S");
            writer.WriteEndElement(); // xs:minInclusive

            writer.WriteStartElement("xs", "maxInclusive", SoapNamespaces.Xsd);
            writer.WriteAttributeString("value", "P10675199DT2H48M5.4775807S");
            writer.WriteEndElement(); // xs:maxInclusive

            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            //

            writer.WriteStartElement("xs", "element", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "guid");
            writer.WriteAttributeString("nillable", "true");
            writer.WriteAttributeString("type", "tns:guid");
            writer.WriteEndElement(); // xs:element

            writer.WriteStartElement("xs", "simpleType", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "guid");

            writer.WriteStartElement("xs", "restriction", SoapNamespaces.Xsd);
            writer.WriteAttributeString("base", "xs:string");

            writer.WriteStartElement("xs", "pattern", SoapNamespaces.Xsd);
            writer.WriteAttributeString("value", @"[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}");
            writer.WriteEndElement(); // xs:pattern

            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            //

            writer.WriteStartElement("xs", "attribute", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "FactoryType");
            writer.WriteAttributeString("type", "xs:QName");
            writer.WriteEndElement(); // xs:attribute

            writer.WriteStartElement("xs", "attribute", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "Id");
            writer.WriteAttributeString("type", "xs:ID");
            writer.WriteEndElement(); // xs:attribute

            writer.WriteStartElement("xs", "attribute", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "Ref");
            writer.WriteAttributeString("type", "xs:IDREF");
            writer.WriteEndElement(); // xs:attribute

            writer.WriteEndElement(); // xs:schema
        }

        public static void WriteStreamSchema(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("xs", "schema", SoapNamespaces.Xsd);
            writer.WriteXmlnsAttribute("xs", SoapNamespaces.Xsd);
            writer.WriteXmlnsAttribute("tns", StreamNs);
            writer.WriteAttributeString("elementFormDefault", "qualified");
            writer.WriteAttributeString("targetNamespace", StreamNs);

            writer.WriteStartElement("xs", "simpleType", SoapNamespaces.Xsd);
            writer.WriteAttributeString("name", "StreamBody");

            writer.WriteStartElement("xs", "restriction", SoapNamespaces.Xsd);
            writer.WriteAttributeString("base", "xs:base64Binary");

            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            writer.WriteEndElement(); // xs:schema
        }

        private static readonly string[] SerElements =
        {
            "anyType", "anyURI", "base64Binary", "boolean", "byte", "dateTime", "decimal", "double", "float",
            "int", "long", "QName", "short", "string", "unsignedByte", "unsignedInt", "unsignedLong", "unsignedShort"
        };
    }
}
