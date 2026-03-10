// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.AzureDevOps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class CreateTestsForWorkItems : AzureDevOpsTask
    {
        [Required]
        public ITaskItem[] WorkItems { get; set; }

        protected override async Task ExecuteCoreAsync(HttpClient client)
        {
            foreach (ITaskItem workItem in WorkItems)
            {
                var jobName = workItem.GetMetadata("JobName");
                var workItemName = workItem.GetMetadata("WorkItemName");
                var testRunId = workItem.GetMetadata("TestRunId");
                var failed = workItem.GetMetadata("Failed") == "true";

                // If the work item failed, check whether there are already real test results
                // reported for it. If so, those results already capture the failure details —
                // marking WorkItemExecution as failed too is redundant and prevents Build Analysis
                // from recognizing all failures as known issues.
                if (failed)
                {
                    bool hasRealResults = await WorkItemHasTestResultsAsync(client, testRunId, jobName, workItemName);
                    if (hasRealResults)
                    {
                        Log.LogMessage(MessageImportance.Normal,
                            $"Work item {workItemName} already has test results reported; marking WorkItemExecution as passed.");
                        failed = false;
                    }
                }

                await CreateFakeTestResultAsync(client, testRunId, jobName, workItemName, failed);
            }
        }

        /// <summary>
        /// Checks whether the test run already contains real test results for this work item.
        /// Real results are posted by the reporter running on the Helix agent before the work item completes.
        /// </summary>
        private async Task<bool> WorkItemHasTestResultsAsync(HttpClient client, string testRunId, string jobName, string workItemName)
        {
            var data = await RetryAsync(
                async () =>
                {
                    using (var req = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{CollectionUri}{TeamProject}/_apis/test/runs/{testRunId}/results?$top=1&$filter=automatedTestStorage eq '{workItemName}'&api-version=6.0"))
                    {
                        using (HttpResponseMessage res = await client.SendAsync(req))
                        {
                            return await ParseResponseAsync(req, res);
                        }
                    }
                });

            if (data != null && data["count"] != null)
            {
                return data.Value<int>("count") > 0;
            }

            return false;
        }

        private async Task<int> CreateFakeTestResultAsync(HttpClient client, string testRunId, string jobName, string workItemFriendlyName, bool failed)
        {
            var testResultData = await RetryAsync(
                async () =>
                {
                    var req =
                        new HttpRequestMessage(
                            HttpMethod.Post,
                            $"{CollectionUri}{TeamProject}/_apis/test/Runs/{testRunId}/results?api-version=5.1-preview.6")
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(
                                    new JArray
                                    {
                                        new JObject
                                        {
                                            ["automatedTestName"] = $"{workItemFriendlyName}.WorkItemExecution",
                                            ["automatedTestStorage"] = workItemFriendlyName,
                                            ["testCaseTitle"] = $"{workItemFriendlyName} Work Item",
                                            ["outcome"] = failed ? "Failed" : "Passed",
                                            ["state"] = "Completed",
                                            ["errorMessage"] = failed ? "The Helix Work Item failed. Often this is due to a test crash. Please see the 'Artifacts' tab above for additional logs." : null,
                                            ["durationInMs"] = 60 * 1000, // Use a non-zero duration so that the graphs look better.
                                            ["comment"] = new JObject
                                            {
                                                ["HelixJobId"] = jobName,
                                                ["HelixWorkItemName"] = workItemFriendlyName,
                                            }.ToString(),
                                        }
                                    }),
                                Encoding.UTF8,
                                "application/json"),
                        };
                    using (req)
                    {
                        using (HttpResponseMessage res = await client.SendAsync(req))
                        {
                            return await ParseResponseAsync(req, res);
                        }
                    }
                });

            if (testResultData != null)
            {
                if ((JArray)testResultData["value"] != null)
                {
                    var testResults = (JArray)testResultData["value"];
                    return (int)testResults.First()["id"];
                }
                return 0;
            }
            return 0;
        }
    }
}
