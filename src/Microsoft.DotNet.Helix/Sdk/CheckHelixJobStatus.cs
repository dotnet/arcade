using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System;
using System.Linq;
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
            var jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            if (FailOnWorkItemFailure)
            {
                string accessTokenSuffix = string.IsNullOrEmpty(this.AccessToken) ? String.Empty : "?access_token={Get this from helix.dot.net}";
                foreach (ITaskItem failedWorkItem in FailedWorkItems)
                {
                    var jobName = failedWorkItem.GetMetadata("JobName");
                    var workItemName = failedWorkItem.GetMetadata("WorkItemName");
                    var consoleUri = failedWorkItem.GetMetadata("ConsoleOutputUri");

                    Log.LogError(FailureCategory.Test, $"Work item {failedWorkItem} in job {jobName} has failed.");
                    Log.LogError(FailureCategory.Test, $"Failure log: {consoleUri}{accessTokenSuffix} .");
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
