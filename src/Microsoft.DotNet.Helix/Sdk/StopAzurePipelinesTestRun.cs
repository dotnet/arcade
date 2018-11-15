using System;
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

        protected override async Task ExecuteCore()
        {
            using (var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"{CollectionUri}{TeamProject}/_apis/test/runs/{TestRunId}?api-version=5.0-preview.2")
            {
                Content = new StringContent(JsonConvert.SerializeObject(new JObject
                {
                    ["state"] = "Completed",
                }), Encoding.UTF8, "application/json"),
            })
            {
                var res = await Client.SendAsync(req);
                res.EnsureSuccessStatusCode();
            }
        }
    }
}
