using System;
using System.Collections.Concurrent;
using System.Xml.Serialization;

namespace SoapCoreServer
{
    internal static class XmlSerializersCache
    {
        public static XmlSerializer GetSerializer(Type type, string name, string ns)
        {
            var key = $"{type.FullName}__{name}__{ns}";

            return Serializers.GetOrAdd(key,
                                        _ =>
                                        {
                                            var rootAttr = new XmlRootAttribute(name)
                                            {
                                                Namespace = ns
                                            };

                                            return new XmlSerializer(type,
                                                                     overrides: null,
                                                                     extraTypes: new[] { typeof(System.Text.Json.JsonElement) },
                                                                     rootAttr,
                                                                     ns);
                                        });
        }

        private static readonly ConcurrentDictionary<string, XmlSerializer> Serializers = new();
    }
}
