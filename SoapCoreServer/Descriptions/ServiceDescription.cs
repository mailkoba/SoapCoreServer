using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.ServiceModel;

namespace IsGa.Soap.Descriptions
{
    internal class ServiceDescription
    {
        public ServiceDescription(Type serviceType)
        {
            var contractDescriptions = new List<ContractDescription>();
            var interfaces = serviceType.GetInterfaces();

            foreach (var contractType in interfaces)
            {
                var typeInfo = contractType.GetTypeInfo();
                var serviceContracts = typeInfo.GetCustomAttributes<ServiceContractAttribute>();

                contractDescriptions.AddRange(
                    serviceContracts.Select(x => new ContractDescription(x, this, contractType)));
            }

            ServiceType = serviceType;
            ContractDescriptions = contractDescriptions;
        }

        public Type ServiceType { get; }
        public IReadOnlyList<ContractDescription> ContractDescriptions { get; }

        public IEnumerable<OperationDescription> OperationDescriptions =>
            ContractDescriptions.SelectMany(c => c.OperationDescriptions);
    }
}
