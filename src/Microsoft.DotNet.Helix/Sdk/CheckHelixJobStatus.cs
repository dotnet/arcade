using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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

        public bool FailOnWorkItemFailure { get; set; } = true;

        public bool FailOnMissionControlTestFailure { get; set; } = false;

        protected override async Task ExecuteCore()
        {
            List<string> jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            await Task.WhenAll(jobNames.Select(CheckHelixJobAsync));
        }

        private async Task CheckHelixJobAsync(string jobName)
        {
            await Task.Yield();
            Log.LogMessage($"Checking status of job {jobName}");
            var workItems = await HelixApi.RetryAsync(
                () => HelixApi.WorkItem.ListAsync(jobName),
                LogExceptionRetry);
            var waitingCount = workItems.Count(wi => wi.State == "Waiting");
            var runningCount = workItems.Count(wi => wi.State == "Running");
            if (waitingCount != 0 || runningCount != 0)
            {
                Log.LogError(
                    $"This task can only be used on completed jobs. There are {waitingCount} waiting and {runningCount} running work items.");
                return;
            }
            if (FailOnWorkItemFailure)
            {
                // determines whether any of the work items failed (fireballed)
                await Task.WhenAll(workItems.Select(wi => CheckForWorkItemFailureAsync(wi.Name, jobName)));
            }

            if (FailOnMissionControlTestFailure)
            {
                for (; ; await Task.Delay(10000)) // delay every time this loop repeats
                {
                    if (await MissionControlTestProcessingDoneAsync(jobName))
                    {
                        break;
                    }

                    Log.LogMessage($"Job {jobName} is still processing xunit results.");
                }
            }
        }

        private async Task<bool> MissionControlTestProcessingDoneAsync(string jobName)
        {
            var results = await HelixApi.Aggregate.JobSummaryAsync(
                groupBy: ImmutableList.Create("job.name"),
                maxResultSets: 1,
                filterName: jobName
            );

            if (results.Count != 1)
            {
                Log.LogError($"Not exactly 1 result from aggregate api for job '{jobName}': {JsonConvert.SerializeObject(results)}");
                return true;
            }

            var data = results[0].Data;
            if (data == null)
            {
                Log.LogError($"No data found in first result for job '{jobName}'.");
                return true;
            }

            if (data.WorkItemStatus.ContainsKey("fail"))
            {
                Log.LogError($"Job '{jobName}' has {data.WorkItemStatus["fail"]} failed work items.");
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
                    Log.LogError($"Job '{jobName}' has no xunit analysis.");
                    return true;
                }

                var pass = xunitAnalysis.Status.GetValueOrDefault("pass", 0);
                var fail = xunitAnalysis.Status.GetValueOrDefault("fail", 0);
                var skip = xunitAnalysis.Status.GetValueOrDefault("skip", 0);
                var total = pass + fail + skip;

                if (fail > 0)
                {
                    Log.LogError($"Job '{jobName}' failed {fail} out of {total} tests.");
                }
                return true;
            }

            return false;
        }

        private async Task CheckForWorkItemFailureAsync(string workItemName, string jobName)
        {
            await Task.Yield();
            try
            {
                var details = await HelixApi.RetryAsync(
                    () => HelixApi.WorkItem.DetailsAsync(jobName, workItemName),
                    LogExceptionRetry);
                if (details.State == "Failed")
                {
                    Log.LogError(
                        $"Work item {workItemName} on job {jobName} has failed with exit code {details.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                Log.LogError($"Unable to get work item status for '{workItemName}', assuming failure. Exception: {ex}");
            }
        }
    }
}
