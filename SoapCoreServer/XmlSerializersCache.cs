﻿using System;
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
                                            return new XmlSerializer(type: type,
                                                                     overrides: null,
                                                                     extraTypes: new[] { typeof (System.Text.Json.JsonElement) },
                                                                     root: new XmlRootAttribute(name)
                                                                     {
                                                                         Namespace = ns
                                                                     },
                                                                     defaultNamespace: ns);
                                        });
        }

        private static readonly ConcurrentDictionary<string, XmlSerializer> Serializers = new();
    }
}
