using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.Helix.Client
{
    partial class Job
    {
        public async Task<JobPassFail> WaitForJobAsync(string job, int pollingIntervalMs = 10000, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(job)) throw new ArgumentNullException(nameof(job));
            if (pollingIntervalMs < 1000) throw new ArgumentOutOfRangeException(nameof(pollingIntervalMs), pollingIntervalMs, "The polling interval cannot be less than 1000.");

            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            for (;; await Task.Delay(pollingIntervalMs, cancellationToken)) // delay every time this loop repeats
            {
                var pf = await Client.RetryAsync(
                    () => PassFailAsync(job, cancellationToken), 
                    e => { },
                    cancellationToken);
                if (pf.Working == 0 && pf.Total != 0)
                {
                    return pf;
                }

                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
