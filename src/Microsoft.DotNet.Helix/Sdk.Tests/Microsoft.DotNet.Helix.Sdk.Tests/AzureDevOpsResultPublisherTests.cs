// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class AzureDevOpsResultPublisherTests
    {
        [Fact]
        public void AttachmentModeDefaultsToFailed()
        {
            var reportingParameters = new AzureDevOpsReportingParameters(
                new Uri("https://dev.azure.com/dnceng-public/"),
                "public",
                "123");

            Assert.Equal(TestResultAttachmentMode.Failed, reportingParameters.TestResultAttachmentMode);
            Assert.Equal(TestResultAttachmentMode.Failed, new JobMonitorOptions().TestResultAttachmentMode);
        }

        [Fact]
        public void JobMonitorUploadParallelismDefaultsToEight()
        {
            Assert.Equal(8, new JobMonitorOptions().TestResultUploadParallelism);
        }

        [Fact]
        public void Constructor_ConfiguresHttpClientTimeoutForLongUploads()
        {
            using var publisher = new AzureDevOpsResultPublisher(
                new AzureDevOpsReportingParameters(
                    new Uri("https://dev.azure.com/dnceng-public/"),
                    "public",
                    "123",
                    "token"),
                NullLogger.Instance);

            FieldInfo field = typeof(AzureDevOpsResultPublisher).GetField("_httpClient", BindingFlags.Instance | BindingFlags.NonPublic);
            var client = Assert.IsType<HttpClient>(field.GetValue(publisher));

            Assert.Equal(TimeSpan.FromMinutes(5), client.Timeout);
        }

        [Theory]
        [InlineData("Passed", true)]
        [InlineData("NotExecuted", true)]
        [InlineData("Inconclusive", true)]
        [InlineData("Failed", false)]
        [InlineData("None", false)]
        public void ComputeAllPassed_SingleResult_OnlyFailedAndNoneCountAsFailure(string result, bool expectedAllPassed)
        {
            var results = new[] { new AggregatedResult(AggregationType.Single, "Test1", 1, result) };

            Assert.Equal(expectedAllPassed, AzureDevOpsResultPublisher.ComputeAllPassed(results));
        }

        [Fact]
        public void ComputeAllPassed_InconclusiveDataDrivenRollup_DoesNotFailTheWorkItem()
        {
            // Mirrors the rollup the aggregator produces for a theory with some passing and some
            // skipped data rows: no data row failed, but the mix isn't a clean pass or skip either.
            var results = new[]
            {
                new AggregatedResult(AggregationType.Single, "Test1", 1, "Passed"),
                new AggregatedResult(AggregationType.DataDriven, "Test2", 1, "Inconclusive"),
            };

            Assert.True(AzureDevOpsResultPublisher.ComputeAllPassed(results));
        }

        [Fact]
        public void ComputeAllPassed_AnyFailedResult_FailsTheWorkItem()
        {
            var results = new[]
            {
                new AggregatedResult(AggregationType.Single, "Test1", 1, "Passed"),
                new AggregatedResult(AggregationType.DataDriven, "Test2", 1, "Failed"),
            };

            Assert.False(AzureDevOpsResultPublisher.ComputeAllPassed(results));
        }

        [Fact]
        public void HttpClientTimeoutIsTransient()
        {
            Assert.True(AzureDevOpsResultPublisher.IsTransientException(
                new OperationCanceledException("The request timed out.", new TimeoutException()),
                CancellationToken.None));
        }

        [Fact]
        public void CallerCancellationIsNotTransient()
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            Assert.False(AzureDevOpsResultPublisher.IsTransientException(
                new OperationCanceledException("The request timed out.", new TimeoutException()),
                cancellation.Token));
        }

        [Fact]
        public void CancellationWithoutTimeoutIsNotTransient()
        {
            Assert.False(AzureDevOpsResultPublisher.IsTransientException(
                new OperationCanceledException(),
                CancellationToken.None));
        }

        [Fact]
        public async Task UploadTestResultsWithCountAsync_BatchesByTopLevelResultCount()
        {
            var handler = new RecordingResultHandler();
            using var publisher = CreatePublisher(handler);
            AggregatedResult[] results =
            [
                CreateDataDrivenResult("First", 600),
                CreateDataDrivenResult("Second", 600),
            ];

            long uploadedCount = await publisher.UploadTestResultsWithCountAsync(results, new { });

            Assert.Equal(2, uploadedCount);
            Assert.Equal(new[] { 2 }, handler.RequestResultCounts);
        }

        [Fact]
        public async Task UploadTestResultsWithCountAsync_SplitsMoreThanOneThousandTopLevelResults()
        {
            var handler = new RecordingResultHandler();
            using var publisher = CreatePublisher(handler);
            AggregatedResult[] results =
            [
                .. Enumerable.Range(0, 1001)
                    .Select(i => new AggregatedResult(AggregationType.Single, $"Test{i}", 1, "Passed"))
            ];

            long uploadedCount = await publisher.UploadTestResultsWithCountAsync(results, new { });

            Assert.Equal(1001, uploadedCount);
            Assert.Equal(new[] { 1000, 1 }, handler.RequestResultCounts);
        }

        private static AzureDevOpsResultPublisher CreatePublisher(HttpMessageHandler handler)
            => new(
                new AzureDevOpsReportingParameters(
                    new Uri("https://dev.azure.com/dnceng-public/"),
                    "public",
                    "123"),
                NullLogger.Instance,
                new HttpClient(handler));

        private static AggregatedResult CreateDataDrivenResult(string name, int subResultCount)
            => new(
                AggregationType.DataDriven,
                name,
                subResultCount,
                "Passed",
                [
                    .. Enumerable.Range(0, subResultCount)
                        .Select(i => new AggregatedResult(AggregationType.Single, $"{name}_{i}", 1, "Passed"))
                ]);

        private sealed class RecordingResultHandler : HttpMessageHandler
        {
            public List<int> RequestResultCounts { get; } = [];

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                using JsonDocument requestBody = JsonDocument.Parse(
                    await request.Content.ReadAsStringAsync(cancellationToken));
                int resultCount = requestBody.RootElement.GetArrayLength();
                RequestResultCounts.Add(resultCount);

                string responseBody = JsonSerializer.Serialize(new
                {
                    value = Enumerable.Range(1, resultCount).Select(id => new { id })
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(responseBody)
                };
            }
        }

    }
}
