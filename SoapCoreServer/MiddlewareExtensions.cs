using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SoapCoreServer
{
    public static class SoapEndpointExtensions
    {
        public static IApplicationBuilder UseSoapEndpoint<T>(this IApplicationBuilder builder,
                                                             string basePath,
                                                             params Endpoint[] endpoints)
        {
            Utils.ValidateBasePath(basePath);

            if (endpoints == null || endpoints.Length == 0)
            {
                throw new ArgumentException("Endpoints not set!");
            }

            return builder.UseMiddleware<SoapEndpointMiddleware>(typeof (T), basePath, endpoints);
        }

        public static IServiceCollection AddSoapExceptionTransformer(this IServiceCollection serviceCollection,
                                                                     Func<Exception, string> transformer)
        {
            serviceCollection.TryAddSingleton(new ExceptionTransformer(transformer));
            return serviceCollection;
        }
    }
}
