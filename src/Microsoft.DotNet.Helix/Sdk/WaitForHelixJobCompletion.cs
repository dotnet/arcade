using Microsoft.Build.Framework;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
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

        protected override async Task ExecuteCore()
        {
            // Wait 1 second to allow helix to register the job creation
            await Task.Delay(1000);

            List<string> jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            await Task.WhenAll(jobNames.Select(WaitForHelixJobAsync));
        }

        private async Task WaitForHelixJobAsync(string jobName)
        {
            await Task.Yield();
            Log.LogMessage(MessageImportance.High, $"Waiting for completion of job {jobName}");

            for (;; await Task.Delay(10000)) // delay every time this loop repeats
            {
                var workItems = await HelixApi.RetryAsync(
                    () => HelixApi.WorkItem.ListAsync(jobName),
                    LogExceptionRetry);
                var waitingCount = workItems.Count(wi => wi.State == "Waiting");
                var runningCount = workItems.Count(wi => wi.State == "Running");
                var finishedCount = workItems.Count(wi => wi.State == "Finished");
                if (waitingCount == 0 && runningCount == 0 && finishedCount > 0)
                {
                    Log.LogMessage(MessageImportance.High, $"Job {jobName} is completed with {finishedCount} finished work items.");
                    return;
                }

                Log.LogMessage($"Job {jobName} is not yet completed with Waiting: {waitingCount}, Running: {runningCount}, Finished: {finishedCount}");
            }
        }
    }
}
