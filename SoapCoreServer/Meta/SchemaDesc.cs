﻿using System;
using System.Collections.Generic;
using System.Linq;
using SoapCoreServer.Descriptions;

namespace SoapCoreServer.Meta
{
    internal class SchemaDesc
    {
        public SchemaDesc(string ns, WsdlDesc wsdlDesc)
        {
            if (string.IsNullOrWhiteSpace(ns))
            {
                throw new ArgumentNullException(nameof(ns));
            }

            Ns = ns;
            WsdlDesc = wsdlDesc ?? throw new ArgumentNullException(nameof(wsdlDesc));

            Elements = new List<ElementDesc>();
            Enums = new List<Type>();
            ComplexTypes = new List<ElementDesc>();
        }

        public IList<ElementDesc> Elements { get; }

        public WsdlDesc WsdlDesc { get; }

        public string Ns { get; }

        public IList<Type> Enums { get; }

        public IList<ElementDesc> ComplexTypes { get; }

        public string[] ImportNs => ComplexTypes.SelectMany(x => x.Children)
                                                .Where(x => x.Ns != Ns && x.Ns != DataContractNs)
                                                .Select(x => x.Ns)
                                                .Distinct()
                                                .ToArray();

        public bool HasSerializationTypes => ComplexTypes.Any(
            x => x.Children
                  .Any(y => y.Type == typeof(Guid) || y.Type == typeof(Guid?)));

        public void AddMethod(OperationDataDescription operation)
        {
            var elem = ElementDesc.CreateRoot(this,
                                              operation.MessageName,
                                              operation.WrapperNamespace ?? Ns,
                                              operation.Type,
                                              operation.AllMembers);

            if (operation.IsWrapped)
            {
                AddElement(elem);
            }
            else
            {
                if (elem.Children.Length > 1)
                {
                    throw new Exception($"Wrong IsWrapped attribute value on type {operation.Type.FullName}!");
                }
            }

            Array.ForEach(elem.Children, ScanElement);
        }

        public void AddStreamMethod(OperationDescription operation)
        {
            var elem = ElementDesc.CreateRoot(this,
                                              operation.Name,
                                              operation.ContractDescription.Namespace,
                                              operation.Request.Type,
                                              new[]
                                              {
                                                  new OperationMemberDescription(operation.Request.Type,
                                                                                 operation.Request.MessageName,
                                                                                 Utils.StreamNs)
                                              });
            AddElement(elem);

            if (operation.IsOneWay) return;

            var elemResponse = ElementDesc.CreateEmptyRoot(this,
                                                           operation.Response.MessageName,
                                                           operation.ContractDescription.Namespace);
            AddElement(elemResponse);
        }

        #region private

        private const string DataContractNs = "http://schemas.datacontract.org/2004/07/System";

        private bool ContainsElement(Type type, string ns = null)
        {
            ns ??= Utils.GetNsByType(type, WsdlDesc.SoapSerializer);
            return WsdlDesc.GetSchema(ns).Elements.Any(x => x.Type == type);
        }

        private void AddEnum(Type type, string ns = null)
        {
            ns ??= Utils.GetNsByType(type, WsdlDesc.SoapSerializer);
            var schema = WsdlDesc.GetSchema(ns);
            if (!schema.Enums.Contains(type))
            {
                schema.Enums.Add(type);
            }
        }

        private void AddComplexType(ElementDesc elem)
        {
            var ns = elem.Ns ?? Utils.GetNsByType(elem.Type, WsdlDesc.SoapSerializer);
            var schema = WsdlDesc.GetSchema(ns);
            if (schema.ComplexTypes.All(x => x.Type != elem.Type))
            {
                schema.ComplexTypes.Add(elem);
            }
        }

        private ElementDesc AddElement(ElementDesc elem)
        {
            var ns = elem.Ns ?? Utils.GetNsByType(elem.Type, WsdlDesc.SoapSerializer);
            var schema = WsdlDesc.GetSchema(ns);
            if (!schema.Elements.Any(x => x.Type == elem.Type && x.Name == elem.Name))
            {
                schema.Elements.Add(elem);
            }
            return elem;
        }

        private void ScanElement(ElementDesc elem)
        {
            if (ContainsElement(elem.Type)) return;

            if (elem.Type.IsValueType || elem.Type == typeof(string))
            {
                if (elem.Type.IsEnum)
                {
                    AddElement(ElementDesc.Create(this, elem.TypeName, elem.Ns, elem.Type, elem.ArrayType, nullable: elem.Nullable));
                    AddEnum(elem.Type, elem.Ns);
                }
            }
            else
            {
                var (type, isArray) = Utils.GetFilteredPropertyType(elem.Type);
                if (isArray)
                {
                    if (type == typeof(byte)) return;

                    var arrayElement = AddElement(
                        ElementDesc.Create(this, elem.TypeName, elem.Ns, elem.Type, elem.ArrayType, elem.TypeName));

                    if (elem.ArrayType == ArrayType.Separated)
                    {
                        AddComplexType(arrayElement);
                    }

                    var name = Utils.GetTypeName(type, WsdlDesc.SoapSerializer);
                    var itemElement = ElementDesc.Create(this,
                                                         name,
                                                         Utils.GetNsByType(type, WsdlDesc.SoapSerializer),
                                                         type,
                                                         ArrayType.None,
                                                         name);

                    arrayElement.SetChildren(itemElement);

                    if (!ContainsElement(type))
                    {
                        if (IsComplexType(type))
                        {
                            var element = ElementDesc.Create(this,
                                                             itemElement.TypeName,
                                                             itemElement.Ns,
                                                             itemElement.Type,
                                                             itemElement.ArrayType,
                                                             itemElement.TypeName);
                            //AddComplexType(element);
                            ScanElement(element);
                        }

                        if (type.IsEnum)
                        {
                            AddEnum(type, itemElement.Ns);
                        }
                    }

                    return;
                }

                if (IsComplexType(type))
                {
                    if (elem.Name != elem.TypeName)
                    {
                        elem = ElementDesc.Create(this, elem.TypeName, elem.Ns, type, elem.ArrayType, elem.TypeName);
                    }

                    AddElement(elem.Clone());
                    AddComplexType(elem);
                }

                var baseType = Utils.GetFilteredPropertyType(elem.Type).type.BaseType;
                var inherited = baseType != null && baseType != typeof(object);

                if (inherited && !ContainsElement(baseType))
                {
                    var baseElement = ElementDesc.Create(this,
                                                         Utils.GetTypeName(baseType, WsdlDesc.SoapSerializer),
                                                         Utils.GetNsByType(baseType, WsdlDesc.SoapSerializer),
                                                         baseType,
                                                         ArrayType.None);
                    ScanElement(baseElement);
                }

                var properties = elem.Type
                                     .GetFieldsAndProperties()
                                     // filter inherited fields and properties
                                     .Where(x => x.DeclaringType == elem.Type)
                                     .Select(x => new
                                     {
                                         Info = Utils.GetMemberInfo(x, WsdlDesc.SoapSerializer),
                                         Name = x.Name,
                                         Type = x.GetMemberType()
                                     })
                                     .Where(x => x.Info != null)
                                     .OrderBy(x => x.Info.Order)
                                     .ThenBy(x => x.Name)
                                     .Select(x =>
                                     {
                                         return ElementDesc.Create(this,
                                                                   x.Info.Name,
                                                                   Utils.GetNsByType(x.Type, WsdlDesc.SoapSerializer),
                                                                   x.Type,
                                                                   x.Info.ArrayType,
                                                                   required: x.Info.Required,
                                                                   nullable: x.Info.Nullable,
                                                                   emitDefaultValue: x.Info.EmitDefaultValue);
                                     })
                                     .ToArray();

                elem.SetChildren(properties);
                Array.ForEach(properties, ScanElement);
            }
        }

        private static bool IsComplexType(Type type)
        {
            return !(type.IsValueType || type == typeof(string));
        }

        #endregion private
    }
}
