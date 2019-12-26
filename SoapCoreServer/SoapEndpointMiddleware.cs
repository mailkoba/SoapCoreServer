using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Threading.Tasks;
using System.ServiceModel.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using IsGa.Soap.BodyWriters;
using IsGa.Soap.Descriptions;
using IsGa.Soap.Encoders;

namespace IsGa.Soap
{
    public class SoapEndpointMiddleware
    {
        public SoapEndpointMiddleware(ILogger<SoapEndpointMiddleware> logger,
                                      RequestDelegate requestDelegate,
                                      Type serviceType,
                                      string basePath,
                                      Endpoint[] endpoints)
        {
            Utils.ValidateBasePath(basePath);

            if (endpoints == null || endpoints.Length == 0)
            {
                throw new ArgumentException("Endpoints not set!");
            }

            _logger = logger;
            _serviceType = serviceType;
            _basePath = basePath;
            _endpoints = endpoints;
            _requestDelegate = requestDelegate;
            _serviceDescription = new ServiceDescription(_serviceType);
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Path.StartsWithSegments(_basePath, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    $"Request for {httpContext.Request.Path} received ({httpContext.Request.ContentLength ?? 0} bytes)");

                if (httpContext.Request.Method?.ToLower() == "get" &&
                    (httpContext.Request.Query.ContainsKey("singleWsdl") ||
                     httpContext.Request.Query.ContainsKey("wsdl")))
                {
                    await ProcessMeta(httpContext);
                }
                else
                {
                    await ProcessOperation(httpContext);
                }
            }
            else
            {
                await _requestDelegate(httpContext);
            }
        }

        #region private

        private const string SoapFaultNs = "http://www.w3.org/2005/08/addressing/soap/fault";
        private const string SoapAction = "SOAPAction";
        private const int MaxSizeOfHeaders = 0x10000;

        private readonly ILogger<SoapEndpointMiddleware> _logger;
        private readonly Type _serviceType;
        private readonly string _basePath;
        private readonly RequestDelegate _requestDelegate;
        private readonly ServiceDescription _serviceDescription;
        private readonly Endpoint[] _endpoints;

        private async Task ProcessMeta(HttpContext httpContext)
        {
            await Task.Run(() =>
            {
                var baseUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}";

                var bodyWriter = new MetaBodyWriter(_serviceDescription, baseUrl, _endpoints);
                var encoder = EncoderFactory.DefaultEncoder;

                var responseMessage = Message.CreateMessage(encoder.MessageVersion, null, bodyWriter);
                responseMessage = new MetaMessage(responseMessage, _serviceDescription);

                httpContext.Response.ContentType = encoder.ContentType;
                encoder.WriteMessage(responseMessage, httpContext.Response.Body);
            });
        }

        private MessageType? GetMessageType(HttpContext httpContext)
        {
            var path = httpContext.Request.Path.Value;
            if (path.Length < _basePath.Length)
            {
                return null;
            }
            var endpointUrl = path.Substring(_basePath.Length, path.Length - _basePath.Length);
            if (string.IsNullOrWhiteSpace(endpointUrl))
            {
                endpointUrl = "/";
            }

            var endpoint = _endpoints.FirstOrDefault(
                x => x.Url.Equals(endpointUrl, StringComparison.OrdinalIgnoreCase));

            return endpoint?.Type;
        }

        private Message CreateResponseMessage(OperationDescription operation,
                                              MessageEncoder messageEncoder,
                                              object value)
        {
            Message responseMessage;
            if (operation.IsOneWay)
            {
                responseMessage = new CustomMessage(
                    Message.CreateMessage(messageEncoder.MessageVersion, null));
            }
            else
            {
                var bodyWriter = new WrappedBodyWriter(
                    operation.Response.WrapperNamespace ?? operation.ContractDescription.Namespace,
                    operation.Response.MessageName,
                    value ?? new object());

                responseMessage = Message.CreateMessage(messageEncoder.MessageVersion, null, bodyWriter);
                responseMessage = new CustomMessage(responseMessage);
            }

            return responseMessage;
        }

        private async Task<Message> ProcessOperation(HttpContext httpContext)
        {
            var messageType = GetMessageType(httpContext);
            if (!messageType.HasValue)
            {
                await ProcessInfoPage(httpContext);
                return null;
            }
            if (messageType.Value.IsStreamed())
            {
                return await ProcessStreamedRequest(httpContext, messageType.Value);
            }
            else
            {
                return await ProcessBufferedRequest(httpContext, messageType.Value);
            }
        }

        private async Task ProcessInfoPage(HttpContext httpContext)
        {
            var pageText = GetInfoPage();
            var partService = _serviceType.Name;
            var partHost =
                $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}";

            pageText = pageText.Replace("[ServiceName]", partService)
                               .Replace("[Url]", partHost);

            httpContext.Response.ContentType = "text/html";

            await httpContext.Response.WriteAsync(pageText);
        }

        private async Task<Message> ProcessStreamedRequest(HttpContext httpContext, MessageType messageType)
        {
            Message responseMessage;
            try
            {
                var messageEncoder = EncoderFactory.Create(messageType);

                CheckContentType(messageEncoder.ContentType, httpContext.Request.ContentType);

                var requestMessage = messageEncoder.ReadMessage(httpContext.Request.Body,
                                                                MaxSizeOfHeaders,
                                                                httpContext.Request.ContentType);

                var soapAction = messageType == MessageType.StreamText
                                     ? httpContext.Request.Headers[SoapAction].Single().Trim('"')
                                     : requestMessage.Headers.Action;

                var operation = _serviceDescription
                                .OperationDescriptions
                                .FirstOrDefault(
                                    x => x.SoapAction.Equals(soapAction, StringComparison.Ordinal) ||
                                         x.Name.Equals(soapAction, StringComparison.Ordinal));

                if (operation == null)
                {
                    throw new InvalidOperationException(
                        $"No operation found for specified action: {soapAction}");
                }

                var serviceInstance = httpContext.RequestServices.GetService(_serviceDescription.ServiceType);
                if (serviceInstance == null)
                {
                    throw new Exception($"Service type {_serviceDescription.ServiceType.Name} not registered!");
                }

                object[] arguments;
                if (operation.IsStreamRequest)
                {
                    Stream stream = new MessageBodyStream(requestMessage,
                                                          operation.Name,
                                                          operation.ContractDescription.Namespace,
                                                          operation.Request.MessageName,
                                                          operation.ContractDescription.Namespace);
                    arguments = new object[] { stream };
                }
                else
                {
                    arguments = operation.IsEmptyRequest
                                    ? new object[0]
                                    : RequestHelper.GetRequestArguments(requestMessage, operation);
                }

                var responseObject = await RunMethod(operation, serviceInstance, arguments);

                if (operation.IsOneWay) return null;

                responseMessage = CreateResponseMessage(operation, messageEncoder, responseObject);

                httpContext.Response.ContentType = httpContext.Request.ContentType;
                httpContext.Response.Headers[SoapAction] = operation.ReplyAction;

                messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);
            }
            catch (Exception exception)
            {
                _logger.LogError(0, exception, exception.Message);
                responseMessage = WriteErrorResponseMessage(exception,
                                                            StatusCodes.Status500InternalServerError,
                                                            httpContext);
            }

            return responseMessage;
        }

        private async Task<Message> ProcessBufferedRequest(HttpContext httpContext, MessageType messageType)
        {
            try
            {
                if (httpContext.Request.ContentLength > 0)
                {
                    var messageEncoder = EncoderFactory.Create(messageType);

                    CheckContentType(messageEncoder.ContentType, httpContext.Request.ContentType);

                    var requestMessage = messageEncoder.ReadMessage(httpContext.Request.Body,
                                                                    MaxSizeOfHeaders,
                                                                    httpContext.Request.ContentType);

                    var soapAction = messageType == MessageType.Text
                                         ? httpContext.Request.Headers[SoapAction].Single().Trim('"')
                                         : requestMessage.Headers.Action;

                    var operation = _serviceDescription.OperationDescriptions
                                                       .FirstOrDefault(
                                                           x => x.SoapAction.Equals(soapAction,
                                                                                    StringComparison.Ordinal) ||
                                                                x.Name.Equals(soapAction, StringComparison.Ordinal));

                    if (operation == null)
                    {
                        throw new InvalidOperationException(
                            $"No operation found for specified action: {soapAction}");
                    }

                    _logger.LogInformation(
                        $"Request for operation {operation.ContractDescription.Name}.{operation.Name} received");

                    var serviceInstance = httpContext.RequestServices.GetRequiredService(_serviceDescription.ServiceType);

                    var arguments = operation.IsEmptyRequest
                                        ? new object[0]
                                        : RequestHelper.GetRequestArguments(requestMessage, operation);

                    var responseObject = await RunMethod(operation, serviceInstance, arguments);

                    if (operation.IsOneWay) return null;

                    var responseMessage = CreateResponseMessage(operation, messageEncoder, responseObject);

                    httpContext.Response.ContentType = httpContext.Request.ContentType;
                    httpContext.Response.Headers[SoapAction] = operation.ReplyAction;

                    messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);

                    return responseMessage;
                }

                var pageText = GetInfoPage();
                var partService = _serviceType.Name;
                var partHost =
                    $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.Path}";

                pageText = pageText.Replace("[ServiceName]", partService)
                                   .Replace("[Url]", partHost);

                httpContext.Response.ContentType = "text/html";

                await httpContext.Response.WriteAsync(pageText);
            }
            catch (Exception exception)
            {
                _logger.LogError(0, exception, exception.Message);
                return WriteErrorResponseMessage(exception,
                                                 StatusCodes.Status500InternalServerError,
                                                 httpContext);
            }

            return null;
        }

        private async Task<object> RunMethod(OperationDescription operation,
                                             object serviceInstance,
                                             object[] arguments)
        {
            var responseObject = operation.DispatchMethod.Invoke(serviceInstance, arguments);
            if (operation.DispatchMethod.ReturnType.IsTask())
            {
                var responseTask = (Task) responseObject;
                await responseTask;
            }
            else if (operation.DispatchMethod.ReturnType.IsValuableTask())
            {
                var responseTask = (Task) responseObject;
                await responseTask;
                responseObject = responseTask.GetType().GetProperty("Result")?.GetValue(responseTask);
            }

            return responseObject;
        }

        private Message WriteErrorResponseMessage(Exception exception, int statusCode, HttpContext httpContext)
        {
            var ex = exception.InnerException ?? exception;

            var transformer = httpContext.RequestServices.GetService<ExceptionTransformer>();
            var errorText = transformer == null ? ex.Message : transformer.Transform(ex);

            var messageType = GetMessageType(httpContext) ?? MessageType.Text;
            var messageEncoder = EncoderFactory.Create(messageType);

            var msgFault = new FaultException(new FaultReason(errorText), new FaultCode("100"), SoapFaultNs)
                .CreateMessageFault();

            var msg = new FaultMessage(msgFault);

            var bodyWriter = new FaultBodyWriter(msg, messageEncoder.MessageVersion.Envelope);
            var responseMessage = Message.CreateMessage(messageEncoder.MessageVersion,
                                                        messageEncoder.MessageVersion.Envelope == EnvelopeVersion.Soap11
                                                            ? null
                                                            : SoapFaultNs,
                                                        bodyWriter);

            httpContext.Response.ContentType = messageEncoder.ContentType;
            httpContext.Response.StatusCode = statusCode;

            messageEncoder.WriteMessage(responseMessage, httpContext.Response.Body);

            return responseMessage;
        }

        private string GetInfoPage()
        {
            string value;
            var type = GetType();
            using (var template = type.GetTypeInfo()
                                      .Assembly
                                      .GetManifestResourceStream(type, "page.html"))
            {
                using (var sr = new StreamReader(template ?? throw new InvalidOperationException("page.html")))
                {
                    value = sr.ReadToEnd();
                }
            }

            return value;
        }

        private void CheckContentType(string originalContentType, string checkedContentType)
        {
            if (originalContentType.Equals(checkedContentType,
                                           StringComparison.OrdinalIgnoreCase)) return;

            var origArray = originalContentType.Split(';');
            var checkArray = checkedContentType.Split(';');

            if (origArray.Length == checkArray.Length)
            {
                var equals = !origArray.Where((t, i) => !t.Trim().Equals(checkArray[i].Trim(),
                                                                         StringComparison.OrdinalIgnoreCase))
                                       .Any();
                if (equals) return;
            }

            throw new Exception($"Content-Type must be {originalContentType}");
        }

        #endregion private
    }
}
