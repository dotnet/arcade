using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public class StartAzurePipelinesTestRun : AzureDevOpsTask
    {
        [Required]
        public string TestRunName { get; set; }

        [Output]
        public int TestRunId { get; set; }
        
        protected override Task ExecuteCoreAsync(HttpClient client)
        {
            return RetryAsync(
                async () =>
                {
                    var req =
                        new HttpRequestMessage(
                            HttpMethod.Post,
                            $"{CollectionUri}{TeamProject}/_apis/test/runs?api-version=5.0-preview.2")
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(
                                    new JObject
                                    {
                                        ["automated"] = true,
                                        ["build"] = new JObject {["id"] = BuildId,},
                                        ["name"] = TestRunName,
                                        ["state"] = "InProgress",
                                    }),
                                Encoding.UTF8,
                                "application/json"),
                        };
                    using (req)
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            var result = await ParseResponseAsync(req, res);
                            if (result != null)
                            {
                                TestRunId = result["id"].ToObject<int>();
                            }
                        }
                    }
                });
        }
    }
}
