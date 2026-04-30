// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.JobMonitor
{
    internal sealed class AzureDevOpsService : IAzureDevOpsService, IDisposable
    {
        // Tag prefix used to identify Azure DevOps test runs created by this monitor for a
        // particular Helix job. The full tag value is "MonitoredJob-{helixJobName}" and is
        // attached to the test run when it is created. This lets us look up which Helix jobs
        // we have already processed without encoding the Helix job name into the run name.
        private const string MonitoredJobTagPrefix = "MonitoredJob-";

        private readonly JobMonitorOptions _options;
        private readonly ILogger _logger;
        private readonly HttpClient _azdoClient;

        public AzureDevOpsService(JobMonitorOptions options, ILogger logger)
        {
            _options = options;
            _logger = logger;
            _azdoClient = new HttpClient();
            InitializeClient();
        }

        internal AzureDevOpsService(JobMonitorOptions options, ILogger logger, HttpClient azdoClient)
        {
            _options = options;
            _logger = logger;
            _azdoClient = azdoClient ?? throw new ArgumentNullException(nameof(azdoClient));
            InitializeClient();
        }

        private void InitializeClient()
        {
            string encodedToken = Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + _options.SystemAccessToken));
            _azdoClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", encodedToken);
            _azdoClient.DefaultRequestHeaders.UserAgent.ParseAdd("dotnet-helix-job-monitor");
        }

        public async Task<IReadOnlyList<AzureDevOpsTimelineRecord>> GetTimelineRecordsAsync(CancellationToken cancellationToken)
        {
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/build/builds/{_options.BuildId}/timeline?api-version=7.1-preview.2", cancellationToken: cancellationToken);
            return data?["records"]?.ToObject<AzureDevOpsTimelineRecord[]>() ?? [];
        }

        public async Task<IReadOnlySet<string>> GetProcessedHelixJobNamesAsync(CancellationToken cancellationToken)
        {
            string buildUri = Uri.EscapeDataString($"vstfs:///Build/Build/{_options.BuildId}");
            JObject data = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?buildUri={buildUri}&api-version=7.1", cancellationToken: cancellationToken);
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (JObject run in (data?["value"] as JArray ?? []).Cast<JObject>())
            {
                int? runId = run.Value<int?>("id");
                string state = run.Value<string>("state");
                if (runId == null || !string.Equals(state, "Completed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string helixJobName = await GetMonitoredHelixJobNameAsync(runId.Value, cancellationToken);
                if (!string.IsNullOrEmpty(helixJobName))
                {
                    processed.Add(helixJobName);
                }
            }

            return processed;
        }

        private async Task<string> GetMonitoredHelixJobNameAsync(int testRunId, CancellationToken cancellationToken)
        {
            JObject run = await SendAsync(HttpMethod.Get, $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs/{testRunId}?api-version=7.1", cancellationToken: cancellationToken);
            if (run?["tags"] is not JArray tags)
            {
                return null;
            }

            foreach (JToken tag in tags)
            {
                string tagName = tag?.Value<string>("name");
                if (!string.IsNullOrEmpty(tagName) && tagName.StartsWith(MonitoredJobTagPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return tagName.Substring(MonitoredJobTagPrefix.Length);
                }
            }

            return null;
        }

        public async Task<int> CreateTestRunAsync(string name, string helixJobName, CancellationToken cancellationToken)
        {
            JObject result = await SendAsync(HttpMethod.Post,
                $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs?api-version=7.1",
                new JObject
                {
                    ["automated"] = true,
                    ["build"] = new JObject { ["id"] = _options.BuildId },
                    ["name"] = name,
                    ["state"] = "InProgress",
                    ["tags"] = new JArray { new JObject { ["name"] = MonitoredJobTagPrefix + helixJobName } },
                },
                cancellationToken: cancellationToken);
            return result?["id"]?.ToObject<int>() ?? 0;
        }

        public async Task CompleteTestRunAsync(int testRunId, CancellationToken cancellationToken)
        {
            await SendAsync(new HttpMethod("PATCH"),
                $"{_options.CollectionUri}{_options.TeamProject}/_apis/test/runs/{testRunId}?api-version=5.0",
                new JObject { ["state"] = "Completed" },
                cancellationToken: cancellationToken);
        }

        public async Task<bool> UploadTestResultsAsync(int testRunId, IReadOnlyList<WorkItemTestResults> results, CancellationToken cancellationToken)
        {
            var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri(_options.CollectionUri, UriKind.Absolute),
                    _options.TeamProject,
                    testRunId.ToString(CultureInfo.InvariantCulture),
                    _options.SystemAccessToken),
                _logger);

            bool allPassed = true;
            foreach (WorkItemTestResults workItem in results)
            {
                _logger.LogInformation("Publishing test results for work item '{WorkItemName}' in job '{JobName}'...", workItem.WorkItemName, workItem.JobName);
                allPassed &= await publisher.UploadTestResultsAsync(
                    workItem.TestResultFiles,
                    new
                    {
                        HelixJobId = workItem.JobName,
                        HelixWorkItemName = workItem.WorkItemName
                    },
                    cancellationToken);
            }

            return allPassed;
        }

        private async Task<JObject> SendAsync(HttpMethod method, string requestUri, JToken body = null, CancellationToken cancellationToken = default)
        {
            return await RetryHelper.RetryAsync(async () =>
            {
                using var request = new HttpRequestMessage(method, requestUri);
                if (body != null)
                {
                    request.Content = new StringContent(body.ToString(Formatting.None), Encoding.UTF8, "application/json");
                }

                using HttpResponseMessage response = await _azdoClient.SendAsync(request, cancellationToken);
                string content = response.Content != null ? await response.Content.ReadAsStringAsync(cancellationToken) : null;
                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Request to {requestUri} failed with {(int)response.StatusCode} {response.ReasonPhrase}. {content}");
                }

                return string.IsNullOrWhiteSpace(content) ? [] : JObject.Parse(content);
            }, cancellationToken);
        }

        public void Dispose() => _azdoClient.Dispose();
    }
}
