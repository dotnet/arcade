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

        [Required]
        public string Source { get; set; }

        [Required]
        public string Type { get; set; }

        [Required]
        public string Build { get; set; }

        protected override async Task ExecuteCore()
        {
            // Wait 1 second to allow helix to register the job creation
            await Task.Delay(1000);

            // We need to set properties to lowercase so that URL matches MC routing.
            // It needs to be done before escaping to not mutate escaping caracters to lower case.
            Source = Uri.EscapeDataString(Source.ToLowerInvariant()).Replace('%', '~');
            Type = Uri.EscapeDataString(Type.ToLowerInvariant()).Replace('%', '~');
            Build = Build.ToLowerInvariant();
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
                if (waitingCount == 0 && runningCount == 0 && finishedCount > 0)
                {
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
                    return "Mission Control (generation of MC link failed -- GitHub HTTP request error)";
                }
                string userName = "";
                try
                {
                    userName = JObject.Parse(githubJson)["login"].ToString();
                }
                catch (JsonException e)
                {
                    Log.LogMessage(MessageImportance.High, "Failed to parse JSON or find value in parsed JSON", e.StackTrace);
                    return "Mission Control (generation of MC link failed -- JSON parsing error)";
                }

                return $"https://mc.dot.net/#/user/{userName}/{Source}/{Type}/{Build}";
            }
        }
    }
}
