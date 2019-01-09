using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public class StopAzurePipelinesTestRun : AzureDevOpsTask
    {
        [Required]
        public int TestRunId { get; set; }

        protected override async Task ExecuteCoreAsync(HttpClient client)
        {
            await RetryAsync(
                async () =>
                {
                    using (var req =
                        new HttpRequestMessage(
                            new HttpMethod("PATCH"),
                            $"{CollectionUri}{TeamProject}/_apis/test/runs/{TestRunId}?api-version=5.0-preview.2")
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(new JObject {["state"] = "Completed",}),
                                Encoding.UTF8,
                                "application/json"),
                        })
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            res.EnsureSuccessStatusCode();
                        }
                    }
                });

            await RetryAsync(
                async () =>
                {
                    using (var req = new HttpRequestMessage(
                        HttpMethod.Get,
                        $"{CollectionUri}{TeamProject}/_apis/test/runs/{TestRunId}?api-version=5.0-preview.2"))
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            var data = await ParseResponseAsync(req, res);
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
                    }
                });
        }
    }
}
