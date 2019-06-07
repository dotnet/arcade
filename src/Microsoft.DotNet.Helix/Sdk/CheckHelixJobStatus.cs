using System;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        public bool FailOnWorkItemFailure { get; set; } = true;

        public bool FailOnMissionControlTestFailure { get; set; } = false;

        protected override async Task ExecuteCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<string> jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            await Task.WhenAll(jobNames.Select(n => CheckHelixJobAsync(n, cancellationToken)));
        }

        private async Task CheckHelixJobAsync(string jobName, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Log.LogMessage($"Checking status of job {jobName}");
            var status = await HelixApi.RetryAsync(
                () => HelixApi.Job.PassFailAsync(jobName),
                LogExceptionRetry,
                cancellationToken);
            if (status.Working > 0)
            {
                Log.LogError(
                    $"This task can only be used on completed jobs. There are {status.Working} of {status.Total} unfinished work items.");
                return;
            }
            if (FailOnWorkItemFailure)
            {
                foreach (string failedWorkItem in status.Failed)
                {
                    var consoleUri = HelixApi.BaseUri.AbsoluteUri.TrimEnd('/') + $"/api/2018-03-14/jobs/{jobName}/workitems/{Uri.EscapeDataString(failedWorkItem)}/console";

                    Log.LogError($"Work item {failedWorkItem} in job {jobName} has failed, logs available here: {consoleUri}.");
                }
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
                    cancellationToken.ThrowIfCancellationRequested();
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
    }
}
