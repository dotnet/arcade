using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Helix.Client.Models;
using Newtonsoft.Json;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class GetFailedWorkItems : HelixTask
    {
        public const int DelayBetweenHelixApiCallsInMs = 500;

        /// <summary>
        /// An array of Helix Jobs for which to get status
        /// </summary>
        [Required]
        public ITaskItem[] Jobs { get; set; }

        [Output]
        public ITaskItem[] FailedWorkItems { get; set; }

        protected override async Task ExecuteCore(CancellationToken cancellationToken)
        {
            FailedWorkItems = (await Task.WhenAll(Jobs.Select(j => GetFailedWorkItemsAsync(j, cancellationToken))).ConfigureAwait(false)).SelectMany(r => r).ToArray();
        }

        private async Task<IEnumerable<ITaskItem>> GetFailedWorkItemsAsync(ITaskItem job, CancellationToken cancellationToken)
        {
            var jobName = job.GetMetadata("Identity");

            Log.LogMessage($"Getting status of job {jobName}");

            var status = await HelixApi.Job.PassFailAsync(jobName, cancellationToken).ConfigureAwait(false);

            if (status.Working > 0)
            {
                Log.LogError(
                    FailureCategory.Build,
                    $"This task can only be used on completed jobs. There are {status.Working} of {status.Total} unfinished work items.");
                return Array.Empty<ITaskItem>();
            }

            List<ITaskItem> failedWorkItemObjects = new List<ITaskItem>();

            foreach (string workItemName in status.Failed)
            {
                string wi = Helpers.CleanWorkItemName(workItemName);

                // copy all job metadata into the new item
                var metadata = job.CloneCustomMetadata();
                metadata["JobName"] = jobName;
                metadata["WorkItemName"] = wi;
                var consoleUri = HelixApi.Options.BaseUri.AbsoluteUri.TrimEnd('/') + $"/api/2019-06-17/jobs/{jobName}/workitems/{Uri.EscapeDataString(wi)}/console";
                metadata["ConsoleOutputUri"] = consoleUri;

                try
                {
                    // Do this serially with a delay because total failure can hit throttling
                    var files = await HelixApi.WorkItem.ListFilesAsync(wi, jobName, cancellationToken).ConfigureAwait(false);

                    if (!string.IsNullOrEmpty(AccessToken))
                    {
                        // Add AccessToken to all file links because the api requires auth if we submitted the job with auth
                        files = files
                                .Select(file => new UploadedFile(file.Name, file.Link + "?access_token=" + AccessToken))
                                .ToImmutableList();
                    }

                    metadata["UploadedFiles"] = JsonConvert.SerializeObject(files).Replace("%", "%25");
                }
                catch (Exception ex)
                {
                    Log.LogWarningFromException(ex);
                }

                failedWorkItemObjects.Add(new TaskItem($"{jobName}/{wi}", metadata));
                await Task.Delay(DelayBetweenHelixApiCallsInMs);
            }
            return failedWorkItemObjects;
        }
    }
}
