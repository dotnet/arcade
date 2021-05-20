using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.AzureDevOps;
using Microsoft.DotNet.Helix.Client.Models;
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

                await CreateFakeTestResultAsync(client, testRunId, jobName, workItemName, failed);
            }
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
                                            ["errorMessage"] = failed ? "The Helix Work Item failed. Often this is due to a test crash or infrastructure failure. See the Helix Test Logs tab in the Results page of Azure DevOps." : null,
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

            var testResults = (JArray)testResultData["value"];
            return (int)testResults.First()["id"];
        }
    }
}
