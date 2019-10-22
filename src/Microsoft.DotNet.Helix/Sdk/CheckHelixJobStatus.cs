using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;

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

        protected override async Task ExecuteCore(CancellationToken cancellationToken)
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
                await Task.WhenAll(jobNames.Select(n => CheckMissionControlTestFailuresAsync(n, cancellationToken)));
            }
        }

        private async Task CheckMissionControlTestFailuresAsync(string jobName, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();


            Log.LogMessage($"Checking mission control test status of job {jobName}");
            for (; ; await Task.Delay(10000, cancellationToken)) // delay every time this loop repeats
            {
                if (await MissionControlTestProcessingDoneAsync(jobName, cancellationToken))
                {
                    break;
                }

                Log.LogMessage($"Job {jobName} is still processing xunit results.");
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async Task<bool> MissionControlTestProcessingDoneAsync(string jobName,
            CancellationToken cancellationToken)
        {
            var results = await HelixApi.Aggregate.JobSummaryAsync(
                groupBy: ImmutableList.Create("job.name"),
                maxResultSets: 1,
                filterName: jobName,
                cancellationToken: cancellationToken);

            if (results.Count != 1)
            {
                Log.LogError(FailureCategory.Infrastructure, $"Not exactly 1 result from aggregate api for job '{jobName}': {JsonConvert.SerializeObject(results)}");
                return true;
            }

            var data = results[0].Data;
            if (data == null)
            {
                Log.LogError(FailureCategory.Infrastructure, $"No data found in first result for job '{jobName}'.");
                return true;
            }

            if (data.WorkItemStatus.ContainsKey("fail"))
            {
                Log.LogError(FailureCategory.Test, $"Job '{jobName}' has {data.WorkItemStatus["fail"]} failed work items.");
                return true;
            }

            if (data.WorkItemStatus.ContainsKey("none"))
            {
                return false;
            }

            var analysis = data.Analysis;
            if (analysis.Any())
            {
                var xunitAnalysis = analysis.FirstOrDefault(a => a.Name == "xunit");
                if (xunitAnalysis == null)
                {
                    Log.LogError(FailureCategory.Test, $"Job '{jobName}' has no xunit analysis.");
                    return true;
                }

                var pass = xunitAnalysis.Status.GetValueOrDefault("pass", 0);
                var fail = xunitAnalysis.Status.GetValueOrDefault("fail", 0);
                var skip = xunitAnalysis.Status.GetValueOrDefault("skip", 0);
                var total = pass + fail + skip;

                if (fail > 0)
                {
                    Log.LogError(FailureCategory.Test, $"Job '{jobName}' failed {fail} out of {total} tests.");
                }
                return true;
            }

            return false;
        }
    }
}
