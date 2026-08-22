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

                // Build a meaningful error message from available diagnostics
                string errorMessage = null;
                if (failed)
                {
                    errorMessage = BuildErrorMessage(workItem, jobName, workItemName);
                }

                await CreateFakeTestResultAsync(client, testRunId, jobName, workItemName, failed, errorMessage);
            }
        }

        private static string BuildErrorMessage(ITaskItem workItem, string jobName, string workItemName)
        {
            var sb = new StringBuilder();

            string exitCode = workItem.GetMetadata("ExitCode");
            if (!string.IsNullOrEmpty(exitCode))
            {
                sb.AppendLine($"Helix work item exited with code {exitCode}.");
            }

            string helixErrors = workItem.GetMetadata("HelixErrors");
            if (!string.IsNullOrEmpty(helixErrors))
            {
                try
                {
                    var errors = JsonConvert.DeserializeObject<string[]>(helixErrors);
                    if (errors?.Length > 0)
                    {
                        sb.AppendLine("Helix errors:");
                        foreach (var error in errors)
                        {
                            sb.AppendLine($"  - {error}");
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore deserialization failures
                }
            }

            string consoleErrorText = workItem.GetMetadata("ConsoleErrorText");
            if (!string.IsNullOrEmpty(consoleErrorText))
            {
                sb.AppendLine();
                sb.AppendLine("Test output:");
                sb.AppendLine(consoleErrorText);
            }

            if (sb.Length == 0)
            {
                // Fallback to original generic message if we couldn't extract anything
                sb.AppendLine("The Helix Work Item failed. Often this is due to a test crash. Please see the 'Artifacts' tab above for additional logs.");
            }

            string consoleUri = workItem.GetMetadata("ConsoleOutputUri");
            if (!string.IsNullOrEmpty(consoleUri))
            {
                sb.AppendLine();
                sb.AppendLine($"Full console log: {consoleUri}");
            }

            return sb.ToString().TrimEnd();
        }

        private async Task<int> CreateFakeTestResultAsync(HttpClient client, string testRunId, string jobName, string workItemFriendlyName, bool failed, string errorMessage)
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
                                            ["errorMessage"] = failed ? errorMessage : null,
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
