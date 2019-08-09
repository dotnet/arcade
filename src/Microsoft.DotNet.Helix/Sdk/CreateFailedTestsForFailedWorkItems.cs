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
    public class CreateFailedTestsForFailedWorkItems : AzureDevOpsTask
    {
        [Required]
        public ITaskItem[] FailedWorkItems { get; set; }

        protected override async Task ExecuteCoreAsync(HttpClient client)
        {
            foreach (ITaskItem failedWorkItem in FailedWorkItems)
            {
                var jobName = failedWorkItem.GetMetadata("JobName");
                var workItemName = failedWorkItem.GetMetadata("WorkItemName");
                var testRunId = failedWorkItem.GetMetadata("TestRunId");

                var testResultId = await CreateFakeTestResultAsync(client, testRunId, jobName, workItemName);

                try
                {
                    var uploadedFiles = JsonConvert.DeserializeObject<List<UploadedFile>>(failedWorkItem.GetMetadata("UploadedFiles"));
                    var text = $"<ul>{string.Join("", uploadedFiles.Select(f => $"<li><a href='{f.Link}' target='_blank'>{f.Name}</a></li>"))}</ul>";
                    await AttachResultFileToTestResultAsync(client, testRunId, testResultId, text);
                }
                catch (Exception ex)
                {
                    Log.LogWarningFromException(ex);
                }
            }
        }

        private async Task AttachResultFileToTestResultAsync(HttpClient client, string testRunId, int testResultId, string text)
        {
            var b64Stream = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            await RetryAsync(
                async () =>
                {
                    var req =
                        new HttpRequestMessage(
                            HttpMethod.Post,
                            $"{CollectionUri}{TeamProject}/_apis/test/Runs/{testRunId}/Results/{testResultId}/attachments?api-version=5.1-preview.1")
                        {
                            Content = new StringContent(
                                JsonConvert.SerializeObject(
                                    new JObject
                                    {
                                        ["attachmentType"] = "GeneralAttachment",
                                        ["fileName"] = "UploadFileResults.html",
                                        ["stream"] = b64Stream,
                                    }),
                                Encoding.UTF8,
                                "application/json"),
                        };
                    using (req)
                    {
                        using (var res = await client.SendAsync(req))
                        {
                            res.EnsureSuccessStatusCode();
                        }
                    }
                });
        }

        private async Task<int> CreateFakeTestResultAsync(HttpClient client, string testRunId, string jobName, string workItemFriendlyName)
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
                                            ["outcome"] = "Failed",
                                            ["state"] = "Completed",
                                            ["errorMessage"] = "The Work Item Failed",
                                            ["comments"] = new JObject
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
