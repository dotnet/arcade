using Microsoft.Build.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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

            List<(string jobName, string queueName)> jobNames = Jobs.Select(j => (j.GetMetadata("Identity"), j.GetMetadata("HelixTargetQueue"))).ToList();

            cancellationToken.ThrowIfCancellationRequested();
            await Task.WhenAll(jobNames.Select(n => WaitForHelixJobAsync(n.jobName, n.queueName, cancellationToken)));
        }

        private async Task WaitForHelixJobAsync(string jobName, string queueName, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Log.LogMessage(MessageImportance.High, $"Waiting for completion of job {jobName} on {queueName}");

            for (;; await Task.Delay(10000, cancellationToken).ConfigureAwait(false)) // delay every time this loop repeats
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pf = await HelixApi.Job.PassFailAsync(jobName, cancellationToken).ConfigureAwait(false);
                if (pf.Working == 0 && pf.Total != 0)
                {
                    Log.LogMessage(MessageImportance.High, $"Job {jobName} on {queueName} is completed with {pf.Total} finished work items.");
                    return;
                }

                Log.LogMessage($"Job {jobName} on {queueName} is not yet completed with Pending: {pf.Working}, Finished: {pf.Total - pf.Working}");
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }
}
