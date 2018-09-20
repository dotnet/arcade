using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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

        protected async override Task ExecuteCore()
        {
            bool finished = false;
            while (!finished)
            {
                finished = await WorkItemsFinished();
            }
        }

        private async Task<bool> WorkItemsFinished()
        {
            for (int i = 0; i < JobIds.Length; i++)
            {
                var workItems = await HelixApi.Aggregate.WorkItemSummaryMethodAsync(new string[] { "job.build" }, filtername: JobIds[i]);
                if (workItems.Count < 1)
                {
                    Log.LogError($"No work itmes found for job {JobIds[i]}");
                    return true;
                }
                int? pass, fail;
                workItems[0].Data.WorkItemStatus.TryGetValue("pass", out pass);
                workItems[0].Data.WorkItemStatus.TryGetValue("fail", out fail);
                if ((pass ?? 0 + fail ?? 0) < WorkItemCounts[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
