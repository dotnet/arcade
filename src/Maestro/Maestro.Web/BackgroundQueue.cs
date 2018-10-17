// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maestro.Web
{
    public class BackgroundQueue : BackgroundService
    {
        private readonly BlockingCollection<Func<Task>> _workItems = new BlockingCollection<Func<Task>>();

        public BackgroundQueue(ILogger<BackgroundQueue> logger)
        {
            Logger = logger;
        }

        public ILogger<BackgroundQueue> Logger { get; }

        public void Post(Func<Task> workItem)
        {
            Logger.LogInformation($"Posted work to BackgroundQueue: {workItem}");
            _workItems.Add(workItem);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get off the synchronous chain from WebHost.Start
            await Task.Yield();
            using (Logger.BeginScope("Processing Background Queue"))
            {
                while (true)
                {
                    try
                    {
                        while (!_workItems.IsCompleted)
                        {
                            if (stoppingToken.IsCancellationRequested)
                            {
                                _workItems.CompleteAdding();
                            }

                            _workItems.TryTake(out Func<Task> item, 1000);
                            if (item != null)
                            {
                                using (Logger.BeginScope("Executing background work: {item}", item.ToString()))
                                {
                                    try
                                    {
                                        await item();
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.LogError(
                                            ex,
                                            "Background work {item} threw an unhandled exception.",
                                            item.ToString());
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Background queue got unhandled exception.");
                        continue;
                    }

                    return;
                }
            }
        }
    }
}
