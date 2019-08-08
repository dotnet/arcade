using System;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public class CheckHelixJobStatus : AzureDevOpsHelixTask
    {
        /// <summary>
        /// An array of Helix Jobs to be checked
        /// </summary>
        [Required]
        public ITaskItem[] Jobs { get; set; }

        public bool FailOnWorkItemFailure { get; set; } = true;

        public bool FailOnMissionControlTestFailure { get; set; } = false;

        protected override async Task ExecuteCoreAsync(HttpClient client, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dictionary<string, string> jobNames = Jobs.ToDictionary(j => j.GetMetadata("Identity"), j => j.GetMetadata("TestRunId"));

            await Task.WhenAll(jobNames.Select(n => CheckHelixJobAsync(client, n.Key, n.Value, cancellationToken)));
        }

        private async Task CheckHelixJobAsync(HttpClient client, string jobName, string testRunId, CancellationToken cancellationToken)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            Log.LogMessage($"Checking status of job {jobName}");
            var status = await HelixApi.RetryAsync(
                () => HelixApi.Job.PassFailAsync(jobName, cancellationToken),
                LogExceptionRetry,
                cancellationToken);
            if (status.Working > 0)
            {
                Log.LogError(
                    $"This task can only be used on completed jobs. There are {status.Working} of {status.Total} unfinished work items.");
                return;
            }
            if (FailOnWorkItemFailure)
            {
                foreach (string failedWorkItem in status.Failed)
                {
                    var consoleUri = HelixApi.BaseUri.AbsoluteUri.TrimEnd('/') + $"/api/2019-06-17/jobs/{jobName}/workitems/{Uri.EscapeDataString(failedWorkItem)}/console";

                    Log.LogError($"Work item {failedWorkItem} in job {jobName} has failed, logs available here: {consoleUri}.");

                    var testResultId = await CreateFakeTestResultAsync(client, testRunId, failedWorkItem);

                    string fileStreamString = await GetUploadedFilesAsync(jobName, failedWorkItem, cancellationToken);
                    if (fileStreamString != null)
                    {
                        await AttachResultFileToTestResultAsync(client, testRunId, testResultId, fileStreamString);
                    }
                }
            }

            if (FailOnMissionControlTestFailure)
            {
                for (; ; await Task.Delay(10000, cancellationToken)) // delay every time this loop repeats
                {
                    if (await MissionControlTestProcessingDoneAsync(jobName, cancellationToken))
                    {
                        break;
                    }

                    Log.LogMessage($"Job {jobName} is still processing xunit results.");
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
        }

        private async Task<string> GetUploadedFilesAsync(string jobName, string workItemFriendlyName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Log.LogMessage($"Looking up files for work item {workItemFriendlyName} in job {jobName}");

            try
            {
                var uploadedFiles = await HelixApi.RetryAsync(
                    () => HelixApi.WorkItem.ListFilesAsync(workItemFriendlyName, jobName, cancellationToken),
                    LogExceptionRetry,
                    cancellationToken);

                if (uploadedFiles.Count > 0)
                {
                    using (var ms = new MemoryStream())
                    {
                        TextWriter tw = new StreamWriter(ms);

                        tw.WriteLine("<ul>");
                        foreach (var uploadedFile in uploadedFiles)
                        {
                            tw.WriteLine($"<li><a href='{uploadedFile.Link}' target='_blank'>{uploadedFile.Name}</a></li>");
                        }
                        tw.WriteLine("</ul>");

                        tw.Flush();
                        ms.Position = 0;
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return null;
            }
        }

        private async Task AttachResultFileToTestResultAsync(HttpClient client, string testRunId, int testResultId, string stream)
        {
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
                                        ["stream"] = stream,
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

        private async Task<int> CreateFakeTestResultAsync(HttpClient client, string testRunId, string workItemFriendlyName)
        {
            var testRunData = await RetryAsync(
                    async () =>
                    {
                        using (var req = new HttpRequestMessage(
                            HttpMethod.Get,
                            $"{CollectionUri}{TeamProject}/_apis/test/runs/{testRunId}/results?api-version=5.0")
                        )
                        {
                            using (HttpResponseMessage res = await client.SendAsync(req))
                            {
                                return await ParseResponseAsync(req, res);
                            }
                        }
                    });

            var failedResults = (JArray)testRunData["value"];
            var automatedTestName = (failedResults != null ? (string)failedResults.First()["automatedTestName"] : string.Empty);

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
                                             ["automatedTestName"] = automatedTestName,
                                             ["testCaseTitle"] = string.Format("{0} Failure Report", workItemFriendlyName),
                                             ["outcome"] = "Error",
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

        private async Task<bool> MissionControlTestProcessingDoneAsync(string jobName,
            CancellationToken cancellationToken)
        {
            var results = await HelixApi.Aggregate.JobSummaryAsync(
                groupBy: ImmutableList.Create("job.name"),
                maxResultSets: 1,
                filterName: jobName,
                cancellationToken: cancellationToken);

            if (results.Count != 1)
            {
                Log.LogError($"Not exactly 1 result from aggregate api for job '{jobName}': {JsonConvert.SerializeObject(results)}");
                return true;
            }

            var data = results[0].Data;
            if (data == null)
            {
                Log.LogError($"No data found in first result for job '{jobName}'.");
                return true;
            }

            if (data.WorkItemStatus.ContainsKey("fail"))
            {
                Log.LogError($"Job '{jobName}' has {data.WorkItemStatus["fail"]} failed work items.");
                return true;
            }

            if (data.WorkItemStatus.ContainsKey("none"))
            {
                return false;
            }

            var analysis = data.Analysis;
            if (analysis.Any())
            {
                var xunitAnalysis = analysis.FirstOrDefault(a => a.Name == "xunit");
                if (xunitAnalysis == null)
                {
                    Log.LogError($"Job '{jobName}' has no xunit analysis.");
                    return true;
                }

                var pass = xunitAnalysis.Status.GetValueOrDefault("pass", 0);
                var fail = xunitAnalysis.Status.GetValueOrDefault("fail", 0);
                var skip = xunitAnalysis.Status.GetValueOrDefault("skip", 0);
                var total = pass + fail + skip;

                if (fail > 0)
                {
                    Log.LogError($"Job '{jobName}' failed {fail} out of {total} tests.");
                }
                return true;
            }

            return false;
        }
    }
}
