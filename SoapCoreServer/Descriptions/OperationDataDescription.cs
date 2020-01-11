using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;

namespace SoapCoreServer.Descriptions
{
    public class OperationDataDescription
    {
        public static OperationDataDescription Create(ParameterInfo parameter)
        {
            if (parameter.ParameterType == typeof (Stream))
            {
                return CreateByStream(parameter);
            }

            var desc = CreateByAttribute(parameter.ParameterType);
            if (desc != null)
            {
                desc.MessageName = desc.MessageName ?? parameter.ParameterType.Name;
            }
            return desc;
        }

        public static OperationDataDescription Create(Type returnType)
        {
            var type = returnType.IsValuableTask()
                ? returnType.GetGenericArguments().First()
                : returnType;

            var desc = CreateByAttribute(type) ?? new OperationDataDescription();
            desc.MessageName = desc.MessageName ?? type.Name;

            return desc;
        }

        public static OperationDataDescription CreateEmptyInputMessage(string contractName, string methodName)
        {
            return new OperationDataDescription
            {
                MessageName = $"{contractName}_{methodName}_InputMessage",
                Body = new OperationMemberDescription[] { },
                Headers = new OperationMemberDescription[] { }
            };
        }

        public static OperationDataDescription CreateEmptyOutputMessage(string methodName)
        {
            return new OperationDataDescription
            {
                MessageName = $"{methodName}Response",
                Body = new OperationMemberDescription[] { },
                Headers = new OperationMemberDescription[] { }
            };
        }

        public Type Type { get; private set; }

        public string WrapperNamespace { get; private set; }

        public bool IsWrapped { get; private set; }

        public OperationMemberDescription[] Body { get; private set; }

        public OperationMemberDescription[] Headers { get; private set; }

        public OperationMemberDescription[] AllMembers => Headers.Union(Body).ToArray();

        public string MessageName { get; private set; }

        private static OperationDataDescription CreateByAttribute(Type type)
        {
            if (!(type.GetCustomAttribute(typeof (MessageContractAttribute)) is MessageContractAttribute attr))
            {
                return null;
            }

            var properties = type.GetFieldsAndProperties();

            return new OperationDataDescription
            {
                MessageName = attr.WrapperName,
                WrapperNamespace = attr.WrapperNamespace,
                IsWrapped = attr.IsWrapped,
                Body = GetMessageBody(properties),
                Headers = GetMessageHeaders(properties),
                Type = type
            };
        }

        private static OperationDataDescription CreateByStream(ParameterInfo parameter)
        {
            return new OperationDataDescription
            {
                MessageName = parameter.Name,
                WrapperNamespace = null,
                Body = GetMessageBody(null),
                Headers = GetMessageHeaders(null),
                Type = parameter.ParameterType
            };
        }

        private static OperationMemberDescription[] GetMessageBody(MemberInfo[] properties)
        {
            if (properties == null || properties.Length == 0) return new OperationMemberDescription[] {};

            var infos = GetAttributesInfo<MessageBodyMemberAttribute>(properties);
            if (infos.Length == 0) return new OperationMemberDescription[] { };

            return infos
                   .OrderBy(x => x.attr.Order)
                   .ThenBy(x => x.attr.Name ?? x.prop.Name)
                   .Select(info => new OperationMemberDescription(type: info.prop.GetMemberType(),
                                                                  name: info.attr.Name ?? info.prop.Name,
                                                                  ns: info.attr.Namespace,
                                                                  order: info.attr.Order))
                   .ToArray();
        }

        private static OperationMemberDescription[] GetMessageHeaders(MemberInfo[] properties)
        {
            if (properties == null || properties.Length == 0) return new OperationMemberDescription[] { };

            var infos = GetAttributesInfo<MessageHeaderAttribute>(properties);
            if (infos.Length == 0) return new OperationMemberDescription[] { };

            return infos.Select(x => new OperationMemberDescription(type: x.prop.GetMemberType(),
                                                                    name: x.attr.Name ?? x.prop.Name,
                                                                    ns: x.attr.Namespace,
                                                                    header: true))
                        .ToArray();
        }

        private static (TAttribute attr, MemberInfo prop)[] GetAttributesInfo<TAttribute>(MemberInfo[] properties)
            where TAttribute : MessageContractMemberAttribute
        {
            return properties
                   .Select(x =>
                               (attr: x.GetCustomAttribute(typeof(TAttribute)) as TAttribute,
                                prop: x))
                   .Where(x => x.attr != null)
                   .ToArray();
        }
    }
}
