using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SoapCoreServer;
using SoapCoreServerWebDemo.SoapService;

namespace SoapCoreServerWebDemo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddScoped<DemoService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseSoapEndpoint<DemoService>("/DemoService",
                                             new Endpoint("/text", MessageType.Text),
                                             new Endpoint("/gzip", MessageType.BinaryGZip),
                                             new Endpoint("/deflate", MessageType.BinaryDeflate),
                                             new Endpoint("/binary", MessageType.Binary),
                                             new Endpoint("/stext", MessageType.StreamText),
                                             new Endpoint("/sgzip", MessageType.StreamBinaryGZip),
                                             new Endpoint("/sdeflate", MessageType.StreamBinaryDeflate),
                                             new Endpoint("/sbinary", MessageType.StreamBinary)
                                            );

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
