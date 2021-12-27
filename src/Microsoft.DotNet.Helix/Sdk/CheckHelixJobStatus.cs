using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class CheckHelixJobStatus : HelixTask
    {
        /// <summary>
        /// An array of Helix Jobs to be checked
        /// </summary>
        [Required]
        public ITaskItem[] Jobs { get; set; }

        [Required]
        public ITaskItem[] FailedWorkItems { get; set; }

        public bool FailOnWorkItemFailure { get; set; } = true;

        public bool FailOnMissionControlTestFailure { get; set; } = false;

        protected override Task ExecuteCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FailOnWorkItemFailure)
            {
                string accessTokenSuffix = string.IsNullOrEmpty(AccessToken) ? "" : "?access_token={Get this from helix.dot.net}";
                foreach (ITaskItem workItem in FailedWorkItems)
                {
                    var failed = workItem.GetMetadata("Failed");
                    if (failed == "true")
                    {
                        var jobName = workItem.GetMetadata("JobName");
                        var workItemName = workItem.GetMetadata("WorkItemName");
                        var consoleUri = workItem.GetMetadata("ConsoleOutputUri");

                        Log.LogError($"Work item {workItemName} in job {jobName} has failed.\nFailure log: {consoleUri}{accessTokenSuffix}");
                    }
                }
            }

            if (FailOnMissionControlTestFailure)
            {
                Log.LogMessage($"Mission Control is deprecated. Please set FailOnMissionControlTestFailure to false.");
            }

            return Task.CompletedTask;
        }
    }
}
