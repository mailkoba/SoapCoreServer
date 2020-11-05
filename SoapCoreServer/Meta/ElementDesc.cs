using System;
using System.IO;
using System.Linq;
using SoapCoreServer.Descriptions;

namespace SoapCoreServer.Meta
{
    internal class ElementDesc
    {
        private ElementDesc()
        {
        }

        public static ElementDesc CreateRoot(SchemaDesc schema,
                                             string name,
                                             string ns,
                                             Type type,
                                             OperationMemberDescription[] members)
        {
            var elem = new ElementDesc
            {
                Schema = schema.WsdlDesc.GetSchema(ns),
                Name = name,
                Type = Utils.GetUnderlyingType(type),
                Ns = ns,
                Root = true,
                IsStreamed = members.Any(x => x.Type == typeof(Stream))
            };

            var children = members.Select(x => Create(schema, x)).ToArray();

            elem.SetChildren(children);

            return elem;
        }

        public static ElementDesc Create(SchemaDesc schema,
                                         string name,
                                         string ns,
                                         Type type,
                                         ArrayType arrayType,
                                         string typeName = null,
                                         bool required = false,
                                         bool nullable = false,
                                         bool emitDefaultValue = true)
        {
            var filteredType = Utils.GetFilteredPropertyType(type);
            return new ElementDesc
            {
                Schema = schema.WsdlDesc.GetSchema(ns),
                Name = name,
                Ns = ns,
                Type = Utils.GetUnderlyingType(type),
                Children = new ElementDesc[] { },
                TypeName = typeName ?? GetTypeName(filteredType,
                                                   schema.WsdlDesc.SoapSerializer,
                                                   arrayType),
                Required = required,
                Nullable = nullable,
                EmitDefaultValue = emitDefaultValue,
                ArrayType = arrayType
            };
        }

        public static ElementDesc CreateEmptyRoot(SchemaDesc schema, string name, string ns)
        {
            return new ElementDesc
            {
                Schema = schema.WsdlDesc.GetSchema(ns),
                Name = name,
                Ns = ns,
                Root = true,
                Children = new ElementDesc[] { }
            };
        }

        public void SetChildren(params ElementDesc[] children)
        {
            Children = children;
            Array.ForEach(children, x => x.Parent = this);
        }

        public ElementDesc Clone()
        {
            return Create(Schema, Name, Ns, Type, ArrayType, TypeName, Required, Nullable, EmitDefaultValue);
        }

        public SchemaDesc Schema { get; private set; }

        public string Ns { get; private set; }

        public string Name { get; private set; }

        public Type Type { get; private set; }

        public string TypeName { get; private set; }

        public bool Root { get; private set; }

        public bool Required { get; private set; }

        public bool Nullable { get; private set; }

        public bool NotWriteInComplexType { get; private set; }

        public bool IsStreamed { get; private set; }

        public ElementDesc[] Children { get; private set; }

        public ElementDesc Parent { get; private set; }

        public bool EmitDefaultValue { get; private set; } = true;

        public ArrayType ArrayType { get; private set; } = ArrayType.None;

        private static ElementDesc Create(SchemaDesc schema, OperationMemberDescription member)
        {
            var filteredType = Utils.GetFilteredPropertyType(member.Type);
            var ns = member.Ns ?? Utils.GetNsByType(member.Type, schema.WsdlDesc.SoapSerializer);

            return new ElementDesc
            {
                Schema = schema.WsdlDesc.GetSchema(ns),
                Name = member.Name,
                Type = Utils.GetUnderlyingType(member.Type),
                Ns = ns,
                Children = new ElementDesc[] { },
                TypeName = GetTypeName(filteredType,
                                       schema.WsdlDesc.SoapSerializer,
                                       member.ArrayType),
                NotWriteInComplexType = member.Header,
                IsStreamed = member.Type == typeof(Stream),
                ArrayType = member.ArrayType
            };
        }

        private static string GetTypeName((Type type, bool isArray) typeInfo,
                                          SoapSerializerType soapSerializer,
                                          ArrayType arrayType)
        {
            if (typeInfo.type == typeof(Stream)) return "StreamBody";

            var name = Utils.GetTypeName(typeInfo.type, soapSerializer);

            switch (arrayType)
            {
                case ArrayType.None:
                    return name;
                case ArrayType.InPlace:
                    return name;
                case ArrayType.Separated:
                    return $"ArrayOf{name.Replace("[]", string.Empty)}";
                default:
                    throw new Exception($"Unknown ArrayType '{arrayType}'!");
            }
        }
    }
}
