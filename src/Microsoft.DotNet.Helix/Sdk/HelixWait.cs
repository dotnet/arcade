using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Newtonsoft.Json;

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

            List<string> jobNames = Jobs.Select(j => j.GetMetadata("Identity")).ToList();

            await Task.WhenAll(jobNames.Select(WaitForHelixJobAsync));
        }

        private async Task WaitForHelixJobAsync(string jobName)
        {
            await Task.Yield();
            string mcUri = GetMissionControlResultUri()
            Log.LogMessage($"Waiting for completion of job {jobName}");
            Log.LogMessage(MessageImportance.High, $"Results will be available from {mcUri}");

            while (true)
            {
                var workItems = await HelixApi.WorkItem.ListAsync(jobName);
                var waitingCount = workItems.Count(wi => wi.State == "Waiting");
                var runningCount = workItems.Count(wi => wi.State == "Running");
                var finishedCount = workItems.Count(wi => wi.State == "Finished");
                if (waitingCount == 0 && runningCount == 0)
                {
                    Log.LogMessage(MessageImportance.High, $"Job {jobName} is completed with {finishedCount} finished work items. Results can be found at {mcUri}");
                    return;
                }

                Log.LogMessage(MessageImportance.High, $"Job {jobName} is not yet completed with Waiting: {waitingCount}, Running: {runningCount}, Finished: {finishedCount}");
                await Task.Delay(10000);
            }
        }

        private string GetMissionControlResultUri()
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "AzureDevOps");
                string githubJson = "";
                try
                {
                    githubJson = client.DownloadString($"https://api.github.com/user?access_token={AccessToken}");
                }
                catch (WebException e)
                {
                    Log.LogMessage(MessageImportance.High, "Failed to connect to GitHub to retrieve username", e.StackTrace);
                    return "Mission Control (generation of MC link failed)";
                }
                JsonTextReader jsonTextReader = new JsonTextReader(new StringReader(githubJson));
                do
                {
                    jsonTextReader.Read();
                } while (!(jsonTextReader.TokenType == JsonToken.PropertyName && (string)jsonTextReader.Value == "login"));
                jsonTextReader.Read();
                string userName = (string)jsonTextReader.Value;

                return $"https://mc.dot.net/#/user/{userName}/builds";
            }
        }
    }
}
