using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel.Channels;
using SoapCoreServer.Descriptions;

namespace SoapCoreServer
{
    internal class RequestHelper
    {
        public static object[] GetRequestArguments(Message requestMessage,
                                                   OperationDescription operationDescription)
        {
            var parameters = operationDescription.DispatchMethod.GetParameters();

            var requestRoot = Activator.CreateInstance(parameters[0].ParameterType);

            FillHeaders(requestMessage, operationDescription, requestRoot);
            FillBody(requestMessage, operationDescription, requestRoot);

            var arguments = new List<object> { requestRoot };

            return arguments.ToArray();
        }

        private static void FillHeaders(Message requestMessage,
                                        OperationDescription operationDescription,
                                        object request)
        {
            var properties = request.GetType()
                                    .GetFieldsAndProperties();

            foreach (var property in properties)
            {
                var header = operationDescription.Request
                                                 .Headers
                                                 .FirstOrDefault(x => x.Name == property.Name &&
                                                                      x.Type == property.GetMemberType());
                if (header == null) continue;

                var index = requestMessage.Headers.FindHeader(header.Name, header.Ns);
                using var xmlReader = requestMessage.Headers.GetReaderAtHeader(index);
                var serializer = new DataContractSerializer(header.Type, header.Name, header.Ns);
                var headerBody = serializer.ReadObject(xmlReader);

                property.SetValue(request, headerBody);
            }
        }

        private static void FillBody(Message requestMessage,
                                     OperationDescription operationDescription,
                                     object request)
        {
            var properties = request.GetType()
                                    .GetFieldsAndProperties();

            using var xmlReader = requestMessage.GetReaderAtBodyContents();
            var serializer = new DataContractSerializer(operationDescription.Request.Type,
                                                        operationDescription.Request.MessageName,
                                                        operationDescription.ContractDescription.Namespace);
            var requestBody = serializer.ReadObject(xmlReader);

            foreach (var property in properties)
            {
                var item = operationDescription.Request
                                               .Body
                                               .FirstOrDefault(x => x.Name == property.Name &&
                                                                    x.Type == property.GetMemberType());
                if (item == null) continue;

                property.SetValue(request,
                                  property.GetValue(requestBody));
            }
        }
    }
}
