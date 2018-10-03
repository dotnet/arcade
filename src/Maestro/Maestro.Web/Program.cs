// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.IO;
using Autofac.Extensions.DependencyInjection;
using Maestro.Contracts;
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

    public static class StringExtensions
    {
        public static (string left, string right) Split2(this string value, char splitOn)
        {
            var idx = value.IndexOf(splitOn);

            if (idx < 0)
            {
                return (value, value.Substring(0, 0));
            }

            return (value.Substring(0, idx), value.Substring(idx + 1));
        }
    }
}
