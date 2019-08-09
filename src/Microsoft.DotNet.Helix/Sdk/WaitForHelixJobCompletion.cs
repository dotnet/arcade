using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class WaitForHelixJobCompletion : HelixTask
    {
        /// <summary>
        /// An array of Helix Jobs to be waited on
        /// </summary>
        [Required]
        public ITaskItem[] Jobs { get; set; }

        protected override async Task ExecuteCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Wait 1 second to allow helix to register the job creation
            await Task.Delay(1000, cancellationToken);

            List<string> jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAll(jobNames.Select(n => WaitForHelixJobAsync(n, cancellationToken)));
        }

        private async Task WaitForHelixJobAsync(string jobName, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Log.LogMessage(MessageImportance.High, $"Waiting for completion of job {jobName}");

            for (;; await Task.Delay(10000, cancellationToken)) // delay every time this loop repeats
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pf = await HelixApi.RetryAsync(
                    () => HelixApi.Job.PassFailAsync(jobName, cancellationToken),
                    LogExceptionRetry,
                    cancellationToken);
                if (pf.Working == 0 && pf.Total != 0)
                {
                    Log.LogMessage(MessageImportance.High, $"Job {jobName} is completed with {pf.Total} finished work items.");
                    return;
                }

                Log.LogMessage($"Job {jobName} is not yet completed with Pending: {pf.Working}, Finished: {pf.Total - pf.Working}");
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
