using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class HelixWait : HelixTask
    {
        /// <summary>
        /// An array of Helix Job IDs to be waited on
        /// </summary>
        [Required]
        public string[] JobIds { get; set; }

        /// <summary>
        /// The number of work items sent to Helix
        /// </summary>
        [Required]
        public int[] WorkItemCounts { get; set; }

        /// <summary>
        /// A return string that contains the status of the work items
        /// </summary>
        [Output]
        public string WorkItemStatus { get; set; }

        protected async override Task ExecuteCore()
        {
            WorkItemStatus = "All work items and tests have completed successfully.";  // If this is not true, this status will be overwritten

            bool finished = false;
            while (!finished)
            {
                finished = await WorkItemsFinished();

                await Task.Delay(10000); // Don't overload that Helix API
            }
        }

        private async Task<bool> WorkItemsFinished()
        {
            for (int i = 0; i < JobIds.Length; i++)
            {
                var workItemSummary = await HelixApi.Aggregate.JobSummaryMethodAsync(new string[] { "job.source" }, 1, filtername:JobIds[i]);
                if (workItemSummary.Count < 1)
                {
                    Log.LogError($"Job {JobIds[i]} not found -- something has gone terribly wrong");
                    return true;
                }

                int? pass, fail;
                workItemSummary[0].Data.WorkItemStatus.TryGetValue("pass", out pass);
                workItemSummary[0].Data.WorkItemStatus.TryGetValue("fail", out fail);

                if ((fail ?? 0) > 0)  // If a work item has failed, we don't need to wait anymore and can just state that a workitem has fireballed
                {
                    Log.LogError("One or more work items failed. See Mission Control for more information.");
                    return true;
                }
                else if ((pass ?? 0) < WorkItemCounts[i])  // if the workitems haven't finished, we need to keep waiting
                {
                    return false;
                }
                else  // if they have finished, we should check to see if any of the tests failed. if they have, we can stop early
                {
                    int? testFailures;
                    workItemSummary[0].Data.Analysis[0].Status.TryGetValue("fail", out testFailures);
                    if ((testFailures ?? 0) > 0)
                    {
                        Log.LogError("One or more tests have failed. See Mission Control for more information.");
                        return true;
                    }
                }
            }
            return true;
        }
    }
}
