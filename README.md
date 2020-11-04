# SoapCoreServer
ASP .Net Core implementation of Soap Server. Supports part of features of WCF (Windows Communication Foundation) in .Net Framework 4.x.

Published on NuGet.org - [https://www.nuget.org/packages/SoapCoreServer/](https://www.nuget.org/packages/SoapCoreServer/).

[![Build Status](https://koba.visualstudio.com/CI/_apis/build/status/mailkoba.SoapCoreServer?branchName=master)](https://koba.visualstudio.com/CI/_build/latest?definitionId=1&branchName=master)

#### Features

* Supports transfer modes: text, binary, stream+text, stream+binary
* Supports compress modes: binary+deflate, binary+gzip, stream+binary+deflate, stream+binary+gzip
* Supports transfer data in request headers (for DataContractSerializer mode only)
* Supports 2 serialization modes: DataContractSerializer and XmlSerializer
* Supports IsOneWay methods (returns void). But not like WCF - when IsOneWay=true returns empty response
* Generate wsdl schema compatible with generated by native WCF (SOAP 1.1 & 1.2, types inheritance, generic types, enums with numeric values)
* Supports async methods
* Supports different content encodings

#### Limitations

* Require declaration of attributes ServiceContract, OperationContract, DataContract, MessageContract for service interfaces
* Require declaration of attributes DataMember, MessageBodyMember, MessageHeaderMember for DataContractSerializer mode
* Require declaration of attributes XmlElement, XmlArray, XmlArrayItem for XmlSerializer mode
* ref/out parameters not supported
* Methods must contains no more than one parameter
* Methods parameters must be of complex types (string, int, long, etc not supported)

#### Usage

1. Service interface

```csharp
[ServiceContract(Namespace = "http://soapcoreserver")]
public interface ISoapService
{
    [OperationContract(
        Action = "http://soapcoreserver/SendRequest",
        ReplyAction = "http://soapcoreserver/SendRequestResponse")]
    Task<SendRequestResponse> SendRequest(SendRequest sendRequest);
}
```

2. Service and data types implementation for DataContractSerializer mode

```csharp
[DataContract(Name = "SendRequest", Namespace = "http://soapcoreserver")]
[MessageContract(WrapperName = "SendRequest", WrapperNamespace = "http://soapcoreserver", IsWrapped = true)]
[XmlType(Namespace = "http://soapcoreserver")]
public class SendRequest
{
    [DataMember(Order = 0)]
    [MessageBodyMember(Namespace = "http://soapcoreserver, Order = 0)]
    public string Message { get; set; }
}

[DataContract(Name = "SendRequestResponse", Namespace = "http://soapcoreserver")]
[MessageContract]
[XmlType(Namespace = "http://soapcoreserver")]
public class SendRequestResponse
{
    [DataMember(Order = 0)]
    [MessageBodyMember(Namespace = "http://soapcoreserver, Order = 0)]
    public string Message { get; set; }
}

public class SoapService : ISoapService
{
    public async Task<SendRequestResponse> SendRequest(SendRequest sendRequest)
    {
        return await Task.FromResult(
            new SendRequestResponse {
                Message = sendRequest.Message
            });
    }
}
```

3. SoapCoreServer register

```csharp
using SoapCoreServer;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        ...
        services.AddScoped<SoapService>();

        // optional
        services.AddSoapExceptionTransformer(exception => exception.Message);
        ...
    }

    public void Configure(IApplicationBuilder app)
    {
        ...
        app.UseSoapEndpoint<SoapService>(new SoapCoreOptions()
                                                 .SetBasePath("/SoapService")
                                                 .AddEndpoint(new Endpoint("/text", MessageType.Text))
                                                 .AddEndpoint(new Endpoint("/gzip", MessageType.StreamBinaryGZip))
                                                 .SetSoapSerializer(SoapSerializerType.DataContractSerializer));
        ...
    }
}
```
#### Platform

.Net Core 2.x, .Net Core 3.x, .Net Framework >= 4.7.

#### License

The software released under the terms of the MIT license.
