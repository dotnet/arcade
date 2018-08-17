using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Maestro.Web
{
    public class BackgroundQueue : BackgroundService
    {
        private readonly BlockingCollection<Func<Task>> _workItems = new BlockingCollection<Func<Task>>();

        public void Post(Func<Task> workItem)
        {
            _workItems.Add(workItem);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Get off the synchronous chain from WebHost.Start
            await Task.Yield();
            while (!_workItems.IsCompleted)
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    _workItems.CompleteAdding();
                }

                _workItems.TryTake(out Func<Task> item, 1000);
                if (item != null)
                {
                    await item();
                }
            }
        }
    }
}
