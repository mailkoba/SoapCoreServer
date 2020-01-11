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
            string resolvedType = null;

            switch (typeName)
            {
                case "Boolean":
                    resolvedType = "xs:boolean";
                    break;
                case "Byte":
                    resolvedType = "xs:unsignedByte";
                    break;
                case "Int16":
                    resolvedType = "xs:short";
                    break;
                case "Int32":
                    resolvedType = "xs:int";
                    break;
                case "Int64":
                    resolvedType = "xs:long";
                    break;
                case "SByte":
                    resolvedType = "xs:byte";
                    break;
                case "UInt16":
                    resolvedType = "xs:unsignedShort";
                    break;
                case "UInt32":
                    resolvedType = "xs:unsignedInt";
                    break;
                case "UInt64":
                    resolvedType = "xs:unsignedLong";
                    break;
                case "Decimal":
                    resolvedType = "xs:decimal";
                    break;
                case "Double":
                    resolvedType = "xs:double";
                    break;
                case "Single":
                    resolvedType = "xs:float";
                    break;
                case "DateTime":
                    resolvedType = "xs:dateTime";
                    break;
                case "Guid":
                    resolvedType = "ser:guid";
                    break;
            }

            if (string.IsNullOrEmpty(resolvedType))
            {
                throw new ArgumentException($".NET type {typeName} cannot be resolved into XML schema type!");
            }

            return resolvedType;
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
            writer.WriteStartElement("xs:schema");
            writer.WriteAttributeString("xmlns:xs", "http://www.w3.org/2001/XMLSchema");
            writer.WriteAttributeString("xmlns:tns", SerializationNs);
            writer.WriteAttributeString("elementFormDefault", "qualified");
            writer.WriteAttributeString("targetNamespace", SerializationNs);

            foreach (var elem in SerElements)
            {
                writer.WriteStartElement("xs:element");
                writer.WriteAttributeString("name", elem);
                writer.WriteAttributeString("nillable", "true");
                writer.WriteAttributeString("type", $"xs:{elem}");
                writer.WriteEndElement(); // xs:element
            }

            // 

            writer.WriteStartElement("xs:element");
            writer.WriteAttributeString("name", "char");
            writer.WriteAttributeString("nillable", "true");
            writer.WriteAttributeString("type", "tns:char");
            writer.WriteEndElement(); // xs:element

            writer.WriteStartElement("xs:simpleType");
            writer.WriteAttributeString("name", "char");
            writer.WriteStartElement("xs:restriction");
            writer.WriteAttributeString("base", "xs:int");
            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            //

            writer.WriteStartElement("xs:element");
            writer.WriteAttributeString("name", "duration");
            writer.WriteAttributeString("nillable", "true");
            writer.WriteAttributeString("type", "tns:duration");
            writer.WriteEndElement(); // xs:element

            writer.WriteStartElement("xs:simpleType");
            writer.WriteAttributeString("name", "duration");

            writer.WriteStartElement("xs:restriction");
            writer.WriteAttributeString("base", "xs:duration");

            writer.WriteStartElement("xs:pattern");
            writer.WriteAttributeString("value", @"\-?P(\d*D)?(T(\d*H)?(\d*M)?(\d*(\.\d*)?S)?)?");
            writer.WriteEndElement(); // xs:pattern

            writer.WriteStartElement("xs:minInclusive");
            writer.WriteAttributeString("value", "-P10675199DT2H48M5.4775808S");
            writer.WriteEndElement(); // xs:minInclusive

            writer.WriteStartElement("xs:maxInclusive");
            writer.WriteAttributeString("value", "P10675199DT2H48M5.4775807S");
            writer.WriteEndElement(); // xs:maxInclusive

            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            //

            writer.WriteStartElement("xs:element");
            writer.WriteAttributeString("name", "guid");
            writer.WriteAttributeString("nillable", "true");
            writer.WriteAttributeString("type", "tns:guid");
            writer.WriteEndElement(); // xs:element

            writer.WriteStartElement("xs:simpleType");
            writer.WriteAttributeString("name", "guid");

            writer.WriteStartElement("xs:restriction");
            writer.WriteAttributeString("base", "xs:string");

            writer.WriteStartElement("xs:pattern");
            writer.WriteAttributeString("value", @"[\da-fA-F]{8}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{4}-[\da-fA-F]{12}");
            writer.WriteEndElement(); // xs:pattern

            writer.WriteEndElement(); // xs:restriction
            writer.WriteEndElement(); // xs:simpleType

            //

            writer.WriteStartElement("xs:attribute");
            writer.WriteAttributeString("name", "FactoryType");
            writer.WriteAttributeString("type", "xs:QName");
            writer.WriteEndElement(); // xs:attribute

            writer.WriteStartElement("xs:attribute");
            writer.WriteAttributeString("name", "Id");
            writer.WriteAttributeString("type", "xs:ID");
            writer.WriteEndElement(); // xs:attribute

            writer.WriteStartElement("xs:attribute");
            writer.WriteAttributeString("name", "Ref");
            writer.WriteAttributeString("type", "xs:IDREF");
            writer.WriteEndElement(); // xs:attribute

            writer.WriteEndElement(); // xs:schema
        }

        public static void WriteStreamSchema(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement("xs:schema");
            writer.WriteAttributeString("xmlns:xs", "http://www.w3.org/2001/XMLSchema");
            writer.WriteAttributeString("xmlns:tns", StreamNs);
            writer.WriteAttributeString("elementFormDefault", "qualified");
            writer.WriteAttributeString("targetNamespace", StreamNs);

            writer.WriteStartElement("xs:simpleType");
            writer.WriteAttributeString("name", "StreamBody");

            writer.WriteStartElement("xs:restriction");
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
