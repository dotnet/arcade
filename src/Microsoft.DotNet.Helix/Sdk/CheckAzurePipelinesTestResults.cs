using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public class CheckAzurePipelinesTestResults : AzureDevOpsTask
    {
        public ITaskItem[] ExpectedTestFailures { get; set; }

        protected override async Task ExecuteCoreAsync(HttpClient client)
        {
            if (ExpectedTestFailures?.Length > 0)
            {
                await ValidateExpectedTestFailuresAsync(client);
                return;
            }
            var data = await RetryAsync(
                async () =>
                {
                    using (var req = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{CollectionUri}{TeamProject}/_apis/test/resultsummarybybuild?buildId={BuildId}&api-version=5.1-preview.2"))
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            return await ParseResponseAsync(req, res);
                        }
                    }
                });
            if (data != null && data["aggregatedResultsAnalysis"] is JObject aggregatedResultsAnalysis &&
                aggregatedResultsAnalysis["resultsByOutcome"] is JObject resultsByOutcome)
            {
                foreach (var property in resultsByOutcome.Properties())
                {
                    var outcome = property.Name.ToLowerInvariant();
                    var outcomeResults = (JObject)property.Value;
                    var count = outcomeResults["count"].ToObject<int>();
                    var message = $"Build has {count} {outcome} tests.";
                    if (outcome == "failed")
                    {
                        Log.LogError(message);
                    }
                    else
                    {
                        Log.LogMessage(MessageImportance.High, message);
                    }
                }
            }
            else
            {
                Log.LogError("Unable to get test report from build.");
            }
        }

        private async Task<int[]> GetRunsAsync(HttpClient client)
        {
            var data = await RetryAsync(
                async () =>
                {
                    using (var req = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{CollectionUri}{TeamProject}/_apis/test/runs?buildUri=vstfs:///Build/Build/{BuildId}&api-version=5.0"))
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            return await ParseResponseAsync(req, res);
                        }
                    }
                });
            if (data != null && data["value"] is JArray runList)
            {
                return runList.OfType<JObject>().Select(o => o["id"].ToObject<int>()).ToArray();
            }

            Log.LogError("Unable to get list of test runs from build.");
            return Array.Empty<int>();
        }

        private async Task ValidateExpectedTestFailuresAsync(HttpClient client)
        {
            var runs = await GetRunsAsync(client);
            foreach (var runId in runs)
            {
                var data = await RetryAsync(
                    async () =>
                    {
                        using (var req = new HttpRequestMessage(
                            HttpMethod.Get,
                            $"{CollectionUri}{TeamProject}/_apis/test/runs/{runId}/results?api-version=5.0&outcomes=Failed")
                        )
                        {
                            using (var res = await client.SendAsync(req))
                            {
                                return await ParseResponseAsync(req, res);
                            }
                        }
                    });

                var failedResults = (JArray) data["value"];
                var expectedFailures = ExpectedTestFailures.Select(i => i.GetMetadata("Identity")).ToHashSet();
                foreach (var failedResult in failedResults)
                {
                    var testName = (string) failedResult["automatedTestName"];
                    if (expectedFailures.Contains(testName))
                    {
                        expectedFailures.Remove(testName);
                        Log.LogMessage($"TestRun {runId}: Test {testName} has failed and was expected to fail.");
                    }
                    else
                    {
                        Log.LogError($"TestRun {runId}: Test {testName} has failed and is not expected to fail.");
                    }
                }

                foreach (var expectedFailure in expectedFailures)
                {
                    Log.LogError($"TestRun {runId}: Test {expectedFailure} was expected to fail but did not fail.");
                }
            }
        }
    }
}
