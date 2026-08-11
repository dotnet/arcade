// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.DotNet.Helix.Sdk.Tests
{
    public class AzureDevOpsResultPublisherTests
    {
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
        public void PrepareTestResults_ComputesAllPassedWithoutSendingRequests()
        {
            int requestCount = 0;
            using var scheduler = new AzureDevOpsRequestScheduler(2, NullLogger.Instance);
            using var client = new HttpClient(new DelegateHandler((_, _) =>
            {
                Interlocked.Increment(ref requestCount);
                return Task.FromResult(EmptyResultResponse());
            }));
            using var publisher = CreatePublisher("1", scheduler, client);

            AzureDevOpsResultPublisher.PreparedTestResults prepared = publisher.PrepareTestResults(
                [Result("passed", "Passed"), Result("failed", "Failed")],
                new { });

            Assert.False(prepared.AllPassed);
            Assert.Equal(0, Volatile.Read(ref requestCount));
        }

        [Fact]
        public async Task Scheduler_BoundsConcurrentBatchesAcrossPublisherCalls()
        {
            const int maximumConcurrency = 2;
            int activeRequests = 0;
            int maximumActiveRequests = 0;
            int requestCount = 0;
            var maximumReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRequests = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var scheduler = new AzureDevOpsRequestScheduler(maximumConcurrency, NullLogger.Instance);
            using var client = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
            {
                int active = Interlocked.Increment(ref activeRequests);
                UpdateMaximum(ref maximumActiveRequests, active);
                if (active == maximumConcurrency)
                {
                    maximumReached.TrySetResult(true);
                }

                Interlocked.Increment(ref requestCount);
                await releaseRequests.Task.WaitAsync(cancellationToken);
                Interlocked.Decrement(ref activeRequests);
                return EmptyResultResponse();
            }));
            using var firstPublisher = CreatePublisher("1", scheduler, client);
            using var secondPublisher = CreatePublisher("2", scheduler, client);

            AzureDevOpsResultPublisher.PreparedTestResults first =
                firstPublisher.PrepareTestResults(CreateResults(2500, "first"), new { });
            AzureDevOpsResultPublisher.PreparedTestResults second =
                secondPublisher.PrepareTestResults(CreateResults(2500, "second"), new { });

            Task<TestResultUploadSummary> firstUpload = firstPublisher.PublishTestResultsAsync(first);
            Task<TestResultUploadSummary> secondUpload = secondPublisher.PublishTestResultsAsync(second);

            await maximumReached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(maximumConcurrency, Volatile.Read(ref maximumActiveRequests));
            releaseRequests.TrySetResult(true);
            await Task.WhenAll(firstUpload, secondUpload);

            Assert.Equal(6, Volatile.Read(ref requestCount));
            Assert.Equal(maximumConcurrency, Volatile.Read(ref maximumActiveRequests));
        }

        [Fact]
        public async Task Attachments_StartAfterResultIdsAndRetainAssociation()
        {
            var requests = new ConcurrentQueue<(string PathAndQuery, string Body)>();
            using var scheduler = new AzureDevOpsRequestScheduler(2, NullLogger.Instance);
            using var client = new HttpClient(new DelegateHandler(async (request, cancellationToken) =>
            {
                string body = request.Content is null
                    ? string.Empty
                    : await request.Content.ReadAsStringAsync(cancellationToken);
                requests.Enqueue((request.RequestUri!.PathAndQuery, body));

                if (request.RequestUri.AbsolutePath.EndsWith("/results", StringComparison.OrdinalIgnoreCase))
                {
                    return JsonResponse("""{"value":[{"id":101,"subResults":[{"id":202}]}]}""");
                }

                return JsonResponse("{}");
            }));
            using var publisher = CreatePublisher("42", scheduler, client);
            var child = new AggregatedResult(
                AggregationType.Single,
                "child",
                1,
                "Failed",
                attachments: [new TestResultAttachment("child.txt", "child")]);
            var parent = new AggregatedResult(
                AggregationType.DataDriven,
                "parent",
                1,
                "Failed",
                subResults: [child],
                attachments: [new TestResultAttachment("parent.txt", "parent")]);

            await publisher.PublishTestResultsAsync(publisher.PrepareTestResults([parent], new { }));

            (string PathAndQuery, string Body)[] recorded = requests.ToArray();
            Assert.Equal(3, recorded.Length);
            Assert.EndsWith("/results?api-version=7.1-preview.6", recorded[0].PathAndQuery);
            Assert.Contains("/results/101/attachments?api-version=7.1-preview.1", recorded[1].PathAndQuery);
            Assert.Contains("/results/101/attachments?testSubResultId=202", recorded[2].PathAndQuery);
            Assert.Equal("parent.txt", JsonDocument.Parse(recorded[1].Body).RootElement.GetProperty("fileName").GetString());
            Assert.Equal("child.txt", JsonDocument.Parse(recorded[2].Body).RootElement.GetProperty("fileName").GetString());
        }

        [Fact]
        public async Task Retry_ReacquiresCapacityAfterBackoff()
        {
            var sequence = new ConcurrentQueue<string>();
            int firstPublisherAttempts = 0;
            var firstAttemptCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var scheduler = new AzureDevOpsRequestScheduler(1, NullLogger.Instance);
            using var client = new HttpClient(new DelegateHandler((request, _) =>
            {
                string run = request.RequestUri!.AbsolutePath.Contains("/runs/1/", StringComparison.Ordinal) ? "first" : "second";
                sequence.Enqueue(run);
                if (run == "first" && Interlocked.Increment(ref firstPublisherAttempts) == 1)
                {
                    firstAttemptCompleted.TrySetResult(true);
                    return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        Content = new StringContent("retry")
                    });
                }

                return Task.FromResult(EmptyResultResponse());
            }));
            using var firstPublisher = CreatePublisher("1", scheduler, client);
            using var secondPublisher = CreatePublisher("2", scheduler, client);

            Task<TestResultUploadSummary> firstUpload = firstPublisher.PublishTestResultsAsync(
                firstPublisher.PrepareTestResults([Result("first", "Passed")], new { }));
            await firstAttemptCompleted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Task<TestResultUploadSummary> secondUpload = secondPublisher.PublishTestResultsAsync(
                secondPublisher.PrepareTestResults([Result("second", "Passed")], new { }));
            await secondUpload.WaitAsync(TimeSpan.FromMilliseconds(750));
            await firstUpload.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(["first", "second", "first"], sequence);
        }

        [Fact]
        public async Task QueuedRequest_CanBeCancelledWithoutBeingSent()
        {
            int requestCount = 0;
            var firstRequestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var scheduler = new AzureDevOpsRequestScheduler(1, NullLogger.Instance);
            using var client = new HttpClient(new DelegateHandler(async (_, cancellationToken) =>
            {
                Interlocked.Increment(ref requestCount);
                firstRequestStarted.TrySetResult(true);
                await releaseFirstRequest.Task.WaitAsync(cancellationToken);
                return EmptyResultResponse();
            }));
            using var firstPublisher = CreatePublisher("1", scheduler, client);
            using var secondPublisher = CreatePublisher("2", scheduler, client);

            Task<TestResultUploadSummary> firstUpload = firstPublisher.PublishTestResultsAsync(
                firstPublisher.PrepareTestResults([Result("first", "Passed")], new { }));
            await firstRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            using var cancellation = new CancellationTokenSource();
            Task<TestResultUploadSummary> secondUpload = secondPublisher.PublishTestResultsAsync(
                secondPublisher.PrepareTestResults([Result("second", "Passed")], new { }),
                cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondUpload);
            releaseFirstRequest.TrySetResult(true);
            await firstUpload;
            await Task.Delay(50);
            Assert.Equal(1, Volatile.Read(ref requestCount));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SuccessfulRateLimitHeader_DelaysAdmissionOfNextRequest(bool useRetryAfter)
        {
            int requestCount = 0;
            using var scheduler = new AzureDevOpsRequestScheduler(1, NullLogger.Instance);
            using var client = new HttpClient(new DelegateHandler((_, _) =>
            {
                var response = EmptyResultResponse();
                if (Interlocked.Increment(ref requestCount) == 1)
                {
                    if (useRetryAfter)
                    {
                        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                            TimeSpan.FromMilliseconds(300));
                    }
                    else
                    {
                        response.Headers.TryAddWithoutValidation("X-RateLimit-Delay", "0.3");
                    }
                }

                return Task.FromResult(response);
            }));
            using var firstPublisher = CreatePublisher("1", scheduler, client);
            using var secondPublisher = CreatePublisher("2", scheduler, client);

            await firstPublisher.PublishTestResultsAsync(
                firstPublisher.PrepareTestResults([Result("first", "Passed")], new { }));

            var stopwatch = Stopwatch.StartNew();
            await secondPublisher.PublishTestResultsAsync(
                secondPublisher.PrepareTestResults([Result("second", "Passed")], new { }));

            Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(200), $"Elapsed: {stopwatch.Elapsed}");
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task UnsuccessfulRateLimitHeader_DelaysAdmissionOfNextRequest(bool useRetryAfter)
        {
            using var scheduler = new AzureDevOpsRequestScheduler(1, NullLogger.Instance);
            using var throttledResponse = new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable);
            if (useRetryAfter)
            {
                throttledResponse.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromMilliseconds(300));
            }
            else
            {
                throttledResponse.Headers.TryAddWithoutValidation("X-RateLimit-Delay", "0.3");
            }

            HttpResponseMessage observed = await scheduler.SendAsync(
                _ => Task.FromResult(throttledResponse),
                CancellationToken.None);
            Assert.Same(throttledResponse, observed);

            var stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage next = await scheduler.SendAsync(
                _ => Task.FromResult(EmptyResultResponse()),
                CancellationToken.None);

            Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(200), $"Elapsed: {stopwatch.Elapsed}");
        }

        [Fact]
        public async Task TooManyRequestsWithoutRetryAfter_DelaysOtherPublishers()
        {
            using var scheduler = new AzureDevOpsRequestScheduler(
                1,
                NullLogger.Instance,
                TimeSpan.FromSeconds(30));
            using HttpResponseMessage throttled = await scheduler.SendAsync(
                _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
                CancellationToken.None);

            int sendCount = 0;
            using var cancellation = new CancellationTokenSource();
            Task<HttpResponseMessage> delayed = scheduler.SendAsync(
                _ =>
                {
                    Interlocked.Increment(ref sendCount);
                    return Task.FromResult(EmptyResultResponse());
                },
                cancellation.Token);

            await Task.Delay(100);

            Assert.False(delayed.IsCompleted);
            Assert.Equal(0, Volatile.Read(ref sendCount));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => delayed);
        }

        [Fact]
        public async Task Dispose_CancelsInFlightSendAndCompletesPendingRequests()
        {
            var scheduler = new AzureDevOpsRequestScheduler(1, NullLogger.Instance);
            var sendStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var sendCancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int pendingSendCount = 0;

            Task<HttpResponseMessage> inFlight = scheduler.SendAsync(
                async cancellationToken =>
                {
                    sendStarted.TrySetResult(true);
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        sendCancelled.TrySetResult(true);
                        throw;
                    }

                    return EmptyResultResponse();
                },
                CancellationToken.None);
            await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Task<HttpResponseMessage> pending = scheduler.SendAsync(
                _ =>
                {
                    Interlocked.Increment(ref pendingSendCount);
                    return Task.FromResult(EmptyResultResponse());
                },
                CancellationToken.None);

            scheduler.Dispose();

            await sendCancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => inFlight);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => pending);
            Assert.Equal(0, Volatile.Read(ref pendingSendCount));
            Assert.Equal(0, scheduler.ActiveRequestCount);
        }

        [Fact]
        public async Task CallerCancellation_RemainsCallerCancellation()
        {
            using var scheduler = new AzureDevOpsRequestScheduler(1, NullLogger.Instance);
            using var cancellation = new CancellationTokenSource();
            var sendStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task<HttpResponseMessage> request = scheduler.SendAsync(
                async cancellationToken =>
                {
                    sendStarted.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return EmptyResultResponse();
                },
                cancellation.Token);

            await sendStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            cancellation.Cancel();

            OperationCanceledException exception =
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
            Assert.Equal(cancellation.Token, exception.CancellationToken);
        }

        private static AzureDevOpsResultPublisher CreatePublisher(
            string runId,
            AzureDevOpsRequestScheduler scheduler,
            HttpClient client)
            => new(
                new AzureDevOpsReportingParameters(
                    new Uri("https://dev.azure.com/dnceng-public/"),
                    "public",
                    runId,
                    "token"),
                NullLogger.Instance,
                scheduler,
                client);

        private static AggregatedResult Result(string name, string outcome)
            => new(AggregationType.Single, name, 1, outcome);

        private static IReadOnlyList<AggregatedResult> CreateResults(int count, string prefix)
            => [.. Enumerable.Range(0, count).Select(i => Result($"{prefix}-{i}", "Passed"))];

        private static HttpResponseMessage EmptyResultResponse()
            => JsonResponse("""{"value":[]}""");

        private static HttpResponseMessage JsonResponse(string json)
            => new(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };

        private static void UpdateMaximum(ref int maximum, int value)
        {
            int observed;
            do
            {
                observed = Volatile.Read(ref maximum);
                if (observed >= value)
                {
                    return;
                }
            }
            while (Interlocked.CompareExchange(ref maximum, value, observed) != observed);
        }

        private sealed class DelegateHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
                => sendAsync(request, cancellationToken);
        }

    }
}
