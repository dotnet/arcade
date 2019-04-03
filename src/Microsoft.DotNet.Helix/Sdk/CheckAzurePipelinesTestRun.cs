using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public class CheckAzurePipelinesTestRun : AzureDevOpsTask
    {
        [Required]
        public int TestRunId { get; set; }

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
                        $"{CollectionUri}{TeamProject}/_apis/test/runs/{TestRunId}?api-version=5.0"))
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            return await ParseResponseAsync(req, res);
                        }
                    }
                });
            if (data != null && data["runStatistics"] is JArray runStatistics)
            {
                var failed = runStatistics.Children()
                    .FirstOrDefault(stat => stat["outcome"]?.ToString() == "Failed");
                if (failed != null)
                {
                    Log.LogError($"Test run {TestRunId} has one or more failing tests.");
                }
                else
                {
                    Log.LogMessage(MessageImportance.Low, $"Test run {TestRunId} has not failed.");
                }
            }
        }

        private async Task ValidateExpectedTestFailuresAsync(HttpClient client)
        {
            var data = await RetryAsync(
                async () =>
                {
                    using (var req = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{CollectionUri}{TeamProject}/_apis/test/runs/{TestRunId}/results?api-version=5.0&outcomes=Failed"))
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            return await ParseResponseAsync(req, res);
                        }
                    }
                });

            var failedResults = (JArray) data["results"];
            var expectedFailures = ExpectedTestFailures.Select(i => i.GetMetadata("Identity")).ToHashSet();
            foreach (var failedResult in failedResults)
            {
                var testName = (string) failedResult["automatedTestName"];
                if (expectedFailures.Contains(testName))
                {
                    expectedFailures.Remove(testName);
                    Log.LogMessage($"TestRun {TestRunId}: Test {testName} has failed and was expected to fail.");
                }
                else
                {
                    Log.LogError($"TestRun {TestRunId}: Test {testName} has failed and is not expected to fail.");
                }
            }

            foreach (var expectedFailure in expectedFailures)
            {
                Log.LogError($"TestRun {TestRunId}: Test {expectedFailure} was expected to fail but did not fail.");
            }
        }
    }
}
