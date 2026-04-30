// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class AzureDevOpsServiceTests
    {
        [Fact]
        public async Task CreateTestRunAsync_PersistsHelixJobTag()
        {
            var handler = new RecordingHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(@"{""id"":123}")
                });
            using var service = new AzureDevOpsService(CreateOptions(), NullLogger.Instance, new HttpClient(handler));

            int testRunId = await service.CreateTestRunAsync("Test Run", "helix-job-1", CancellationToken.None);

            Assert.Equal(123, testRunId);
            HttpRequestMessage request = Assert.Single(handler.Requests);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://dev.azure.com/dnceng-public/public/_apis/test/runs?api-version=7.1", request.RequestUri.ToString());

            JObject body = JObject.Parse(Assert.Single(handler.Bodies));
            Assert.Equal("Test Run", body.Value<string>("name"));
            Assert.Equal("InProgress", body.Value<string>("state"));
            Assert.Equal("1403994", body["build"]?.Value<string>("id"));
            Assert.Null(body.Value<string>("comment"));
            Assert.Equal("MonitoredJob-helix-job-1", body["tags"]?[0]?.Value<string>("name"));
        }

        [Fact]
        public async Task GetProcessedHelixJobNamesAsync_ReadsCompletedRunsByMonitoredJobTag()
        {
            var responses = new Queue<string>([
                @"{""value"":[{""id"":10,""state"":""Completed""},{""id"":11,""state"":""InProgress""}]}",
                @"{""id"":10,""tags"":[{""name"":""MonitoredJob-helix-job-1""}]}"
            ]);
            var handler = new RecordingHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responses.Dequeue())
                });
            using var service = new AzureDevOpsService(CreateOptions(), NullLogger.Instance, new HttpClient(handler));

            IReadOnlySet<string> processed = await service.GetProcessedHelixJobNamesAsync(CancellationToken.None);

            Assert.Equal(["helix-job-1"], processed);
            Assert.Equal(2, handler.Requests.Count);
            Assert.EndsWith("/_apis/test/runs?buildUri=vstfs%3A%2F%2F%2FBuild%2FBuild%2F1403994&api-version=7.1", handler.Requests[0].RequestUri.ToString());
            Assert.EndsWith("/_apis/test/runs/10?api-version=7.1", handler.Requests[1].RequestUri.ToString());
        }

        [Fact]
        public async Task GetProcessedHelixJobNamesAsync_ReadsCompletedRunsByUploadedResultMetadata()
        {
            var responses = new Queue<string>([
                @"{""value"":[{""id"":10,""state"":""Completed""},{""id"":11,""state"":""Completed""}]}",
                @"{""id"":10}",
                "{\"value\":[{\"comment\":\"{\\\"HelixJobId\\\":\\\"helix-job-from-result\\\",\\\"HelixWorkItemName\\\":\\\"work-item\\\"}\"}]}",
                @"{""id"":11}",
                @"{""value"":[{""comment"":""not json""}]}"
            ]);
            var handler = new RecordingHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responses.Dequeue())
                });
            using var service = new AzureDevOpsService(CreateOptions(), NullLogger.Instance, new HttpClient(handler));

            IReadOnlySet<string> processed = await service.GetProcessedHelixJobNamesAsync(CancellationToken.None);

            Assert.Equal(["helix-job-from-result"], processed);
            Assert.Equal(5, handler.Requests.Count);
            Assert.EndsWith("/_apis/test/runs/10?api-version=7.1", handler.Requests[1].RequestUri.ToString());
            Assert.EndsWith("/_apis/test/runs/10/results?$top=1&api-version=7.1-preview.6", handler.Requests[2].RequestUri.ToString());
            Assert.EndsWith("/_apis/test/runs/11?api-version=7.1", handler.Requests[3].RequestUri.ToString());
            Assert.EndsWith("/_apis/test/runs/11/results?$top=1&api-version=7.1-preview.6", handler.Requests[4].RequestUri.ToString());
        }

        [Fact]
        public async Task CompleteTestRunAsync_UsesTestRunsApiVersionThatSupportsTags()
        {
            var handler = new RecordingHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                });
            using var service = new AzureDevOpsService(CreateOptions(), NullLogger.Instance, new HttpClient(handler));

            await service.CompleteTestRunAsync(123, CancellationToken.None);

            HttpRequestMessage request = Assert.Single(handler.Requests);
            Assert.Equal(new HttpMethod("PATCH"), request.Method);
            Assert.Equal("https://dev.azure.com/dnceng-public/public/_apis/test/runs/123?api-version=7.1", request.RequestUri.ToString());

            JObject body = JObject.Parse(Assert.Single(handler.Bodies));
            Assert.Equal("Completed", body.Value<string>("state"));
        }

        private static JobMonitorOptions CreateOptions()
            => new()
            {
                BuildId = "1403994",
                CollectionUri = "https://dev.azure.com/dnceng-public/",
                TeamProject = "public",
                SystemAccessToken = "token",
            };

        private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
        {
            public List<HttpRequestMessage> Requests { get; } = [];

            public List<string> Bodies { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (request.Content != null)
                {
                    Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
                }

                return respond(request);
            }
        }
    }
}
