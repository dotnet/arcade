using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Encodings.Web;
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

        public string Creator { get; set; }

        public bool FailOnWorkItemFailure { get; set; } = true;

        protected override async Task ExecuteCore()
        {
            if (string.IsNullOrEmpty(AccessToken) && string.IsNullOrEmpty(Creator))
            {
                Log.LogError("Creator is required when using anonymous access.");
                return;
            }

            if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(Creator))
            {
                Log.LogError("Creator is forbidden when using authenticated access.");
                return;
            }

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
                var workItems = await HelixApi.RetryAsync(
                    () => HelixApi.WorkItem.ListAsync(jobName),
                    LogExceptionRetry);
                var waitingCount = workItems.Count(wi => wi.State == "Waiting");
                var runningCount = workItems.Count(wi => wi.State == "Running");
                var finishedCount = workItems.Count(wi => wi.State == "Finished");
                if (waitingCount == 0 && runningCount == 0 && finishedCount > 0)
                {
                    if (FailOnWorkItemFailure)
                    {
                        // determines whether any of the work items failed (fireballed)
                        await Task.WhenAll(workItems.Select(wi => CheckForWorkItemFailureAsync(wi.Name, jobName)));
                    }

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
            var creator = Creator;
            if (string.IsNullOrEmpty(creator))
            {
                using (var client = new HttpClient
                {
                    DefaultRequestHeaders =
                    {
                        UserAgent = { Helpers.UserAgentHeaderValue },
                    },
                })
                {
                    try
                    {
                        string githubJson =
                            await client.GetStringAsync($"https://api.github.com/user?access_token={AccessToken}");
                        var data = JObject.Parse(githubJson);
                        if (data["login"] == null)
                        {
                            throw new Exception("Github user has no login");
                        }

                        creator = data["login"].ToString();
                    }
                    catch (Exception ex)
                    {
                        Log.LogMessage(MessageImportance.High, "Failed to retrieve username from GitHub -- {0}", ex.ToString());
                        return $"Mission Control (generation of MC link failed -- {ex.Message})";
                    }
                }
            }

            var build = UrlEncoder.Default.Encode(Build).Replace('%', '~');
            var type = UrlEncoder.Default.Encode(Type).Replace('%', '~');
            var source = UrlEncoder.Default.Encode(Source).Replace('%', '~');
            var encodedCreator = UrlEncoder.Default.Encode(creator).Replace('%', '~');
            return $"https://mc.dot.net/#/user/{encodedCreator}/{source}/{type}/{build}";
        }
    }
}
