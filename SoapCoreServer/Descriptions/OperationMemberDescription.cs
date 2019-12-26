using System;

namespace IsGa.Soap.Descriptions
{
    public class OperationMemberDescription
    {
        public OperationMemberDescription(Type type, string name, string ns, int? order = null, bool header = false)
        {
            Type = type;
            Name = name;
            Ns = ns;
            Order = order;
            Header = header;
        }

        public bool Header { get; }

        public string Ns { get; }

        public int? Order { get; }

        public Type Type { get; }

        public string Name { get; }
    }
}
