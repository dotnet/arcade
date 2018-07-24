using System;
using System.IO;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.ServiceFabric.ServiceHost;
using Microsoft.Extensions.Logging;

namespace Maestro.Web
{
    internal static class Program
    {
        public static bool RunningInServiceFabric()
        {
            string fabricApplication = Environment.GetEnvironmentVariable("Fabric_ApplicationName");
            return !string.IsNullOrEmpty(fabricApplication);
        }

        /// <summary>
        ///     This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            if (RunningInServiceFabric())
            {
                ServiceFabricMain();
            }
            else
            {
                NonServiceFabricMain();
            }
        }

        private static void NonServiceFabricMain()
        {
            new WebHostBuilder().UseKestrel()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .ConfigureServices(services => { services.AddAutofac(); })
                .ConfigureLogging(
                    builder =>
                    {
                        builder.AddFilter(level => level > LogLevel.Debug);
                        builder.AddConsole();
                    })
                .UseStartup<Startup>()
                .UseUrls("http://localhost:8080/")
                .CaptureStartupErrors(true)
                .Build()
                .Run();
        }

        private static void ServiceFabricMain()
        {
            ServiceHost.Run(host => host.RegisterStatelessWebService<Startup>("Maestro.WebType"));
        }
    }
}
