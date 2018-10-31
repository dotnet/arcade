using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class HelixWait : HelixTask
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

            string mcUri = await GetMissionControlResultUri();
            Log.LogMessage(MessageImportance.High, $"Results will be available from {mcUri}");

            List<string> jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            await Task.WhenAll(jobNames.Select(WaitForHelixJobAsync));
        }

        private async Task WaitForHelixJobAsync(string jobName)
        {
            await Task.Yield();
            Log.LogMessage($"Waiting for completion of job {jobName}");

            while (true)
            {
                var workItems = await HelixApi.WorkItem.ListAsync(jobName);
                var waitingCount = workItems.Count(wi => wi.State == "Waiting");
                var runningCount = workItems.Count(wi => wi.State == "Running");
                var finishedCount = workItems.Count(wi => wi.State == "Finished");
                if (waitingCount == 0 && runningCount == 0)
                {
                    try
                    {
                        var workItemSummary = await HelixApi.Aggregate.JobSummaryMethodAsync(new string[] { "job.source" }, 1, filtername: jobName);
                        
                        int? numWorkItemFailures;
                        workItemSummary[0].Data.WorkItemStatus.TryGetValue("fail", out numWorkItemFailures);

                        if ((numWorkItemFailures ?? 0) > 0)
                        {
                            Log.LogError($"Job {jobName} had {numWorkItemFailures} work item(s) fail.");
                        }

                        int? numTestFailures;
                        workItemSummary[0].Data.Analysis[0].Status.TryGetValue("fail", out numTestFailures);
                        if ((numTestFailures ?? 0) > 0)
                        {
                            Log.LogError($"Job {jobName} had {numTestFailures} test failures.");
                        }

                        Log.LogMessage(MessageImportance.High, $"Job {jobName} is completed with {finishedCount} finished work items.");
                    }
                    catch (HttpRequestException e)
                    {
                        Log.LogError($"An error was encountered while attempting to query the Helix API for job {jobName}.\n\n{e.StackTrace}");
                    }
                    catch (IndexOutOfRangeException e)
                    {
                        Log.LogError($"The API returned no aggregated work item summary for job {jobName}.\n\n{e.StackTrace}");
                    }

                    Log.LogMessage(MessageImportance.High, $"Job {jobName} is completed with {finishedCount} finished work items.");
                    return;
                }

                Log.LogMessage($"Job {jobName} is not yet completed with Waiting: {waitingCount}, Running: {runningCount}, Finished: {finishedCount}");
                await Task.Delay(10000);
            }
        }

        private async Task<string> GetMissionControlResultUri()
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "AzureDevOps");
                string githubJson = "";
                try
                {
                    githubJson = await client.GetStringAsync($"https://api.github.com/user?access_token={AccessToken}");
                }
                catch (HttpRequestException e)
                {
                    Log.LogMessage(MessageImportance.High, "Failed to connect to GitHub to retrieve username", e.StackTrace);
                    return "Mission Control (generation of MC link failed)";
                }
                string userName = JObject.Parse(githubJson)["login"].ToString();

                return $"https://mc.dot.net/#/user/{userName}/builds";
            }
        }
    }
}
