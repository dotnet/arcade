using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

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

        public bool IsExternal { get; set; } = false;

        public string Creator { get; set; }

        protected override async Task ExecuteCore()
        {
            // Wait 1 second to allow helix to register the job creation
            await Task.Delay(1000);

            // We need to set properties to lowercase so that URL matches MC routing.
            // It needs to be done before escaping to not mutate escaping caracters to lower case.
            Creator = Uri.EscapeDataString(Creator.ToLowerInvariant()).Replace('%', '~');
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
                var workItems = await HelixApi.RetryAsync(
                    () => HelixApi.WorkItem.ListAsync(jobName),
                    LogExceptionRetry);
                var waitingCount = workItems.Count(wi => wi.State == "Waiting");
                var runningCount = workItems.Count(wi => wi.State == "Running");
                var finishedCount = workItems.Count(wi => wi.State == "Finished");
                if (waitingCount == 0 && runningCount == 0 && finishedCount > 0)
                {
                    // determines whether any of the work items failed (fireballed)
                    await Task.WhenAll(workItems.Select(wi => CheckForWorkItemFailureAsync(wi.Name, jobName)));
                    Log.LogMessage(MessageImportance.High, $"Job {jobName} is completed with {finishedCount} finished work items.");
                    return;
                }

                Log.LogMessage($"Job {jobName} is not yet completed with Waiting: {waitingCount}, Running: {runningCount}, Finished: {finishedCount}");
                await Task.Delay(10000);
            }
        }

        private void LogExceptionRetry(Exception ex)
        {
            Log.LogMessage(MessageImportance.Low, $"Checking for job completion failed with: {ex}\nRetrying...");
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

        private async Task<string> GetMissionControlResultUri()
        {
            using (HttpClient client = new HttpClient())
            {
                if (IsExternal)
                {
                    Log.LogMessage($"Job recognized as external. Using Creator property ('{Creator}') in MC link.");
                    if (string.IsNullOrEmpty(Creator))
                    {
                        Log.LogMessage(MessageImportance.High, $"Creator not specified for an anonymous job.");
                        return "Mission Control (link generation failed -- creator not specified for anonymous job)";
                    }
                    else
                    {
                        return $"https://mc.dot.net/#/user/{Creator}/{Source}/{Type}/{Build}";
                    }
                }
                else
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
}
