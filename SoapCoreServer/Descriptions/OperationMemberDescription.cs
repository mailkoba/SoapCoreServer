using System;

namespace SoapCoreServer.Descriptions
{
    public class OperationMemberDescription
    {
        public OperationMemberDescription(Type type,
                                          string name,
                                          string ns,
                                          int? order = null,
                                          bool header = false,
                                          bool isNullable = false,
                                          string dataType = null)
        {
            Type = type;
            Name = name;
            Ns = ns;
            Order = order;
            Header = header;
            IsNullable = isNullable;
            DataType = dataType;
        }

        public bool Header { get; }

        public string Ns { get; }

        public int? Order { get; }

        public Type Type { get; }

        public string Name { get; }

        public bool IsNullable { get; }

        public string DataType { get; }
    }
}
