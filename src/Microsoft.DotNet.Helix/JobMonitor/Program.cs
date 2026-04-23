// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .SetMinimumLevel(LogLevel.Information)
                    .AddSimpleConsole(options =>
                    {
                        options.SingleLine = true;
                        options.TimestampFormat = "[HH:mm:ss] ";
                        options.IncludeScopes = false;
                    });
            });

            ILogger<JobMonitorRunner> logger = loggerFactory.CreateLogger<JobMonitorRunner>();

            try
            {
                JobMonitorOptions options = JobMonitorOptions.Parse(args);
                if (options.ShowHelp)
                {
                    return 0;
                }

                using JobMonitorRunner runner = new(options, logger);
                return await runner.RunAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Helix Job Monitor terminated with an unhandled exception.");
                return 1;
            }
        }
    }
}
