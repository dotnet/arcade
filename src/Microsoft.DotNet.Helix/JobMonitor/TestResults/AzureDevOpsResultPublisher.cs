// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Net.Http.Headers;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Net.Sockets;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.DotNet.Helix.JobMonitor;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

internal sealed class AzureDevOpsResultPublisher : IDisposable
{
    private const int DefaultAttemptCount = 10;
    // Azure DevOps rejects requests containing more than 1,000 top-level TestCaseResult objects.
    // Nested sub-results do not count toward this limit.
    private const int MaximumResultsPerRequest = 1000;

    // Preserve the legacy bound on the recursive size of one result hierarchy independently
    // from the top-level per-request limit.
    private const int MaximumNodesPerResultHierarchy = 950;
    private static readonly TimeSpan s_maximumRetryDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan s_httpClientTimeout = TimeSpan.FromMinutes(5);
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private readonly AzureDevOpsReportingParameters _azdoParameters;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly bool _ownsHttpClient;
    private readonly AzureDevOpsRateLimitGate _rateLimitGate;
    private readonly JobMonitorMetrics _metrics;

    public AzureDevOpsResultPublisher(
        AzureDevOpsReportingParameters azdoParameters,
        ILogger logger)
    {
        _azdoParameters = azdoParameters;
        _httpClient = CreateHttpClient(azdoParameters.AccessToken);
        _logger = logger;
        _metrics = new JobMonitorMetrics();
        _rateLimitGate = new AzureDevOpsRateLimitGate(_metrics);
        _ownsHttpClient = true;
    }

    internal AzureDevOpsResultPublisher(
        AzureDevOpsReportingParameters azdoParameters,
        ILogger logger,
        HttpClient httpClient,
        AzureDevOpsRateLimitGate? rateLimitGate = null,
        JobMonitorMetrics? metrics = null)
        : this(
            azdoParameters,
            logger,
            httpClient,
            rateLimitGate ?? new AzureDevOpsRateLimitGate(),
            metrics ?? new JobMonitorMetrics(),
            ownsHttpClient: false)
    {
    }

    private AzureDevOpsResultPublisher(
        AzureDevOpsReportingParameters azdoParameters,
        ILogger logger,
        HttpClient httpClient,
        AzureDevOpsRateLimitGate rateLimitGate,
        JobMonitorMetrics metrics,
        bool ownsHttpClient)
    {
        _azdoParameters = azdoParameters;
        _httpClient = httpClient;
        _logger = logger;
        _rateLimitGate = rateLimitGate;
        _metrics = metrics;
        _ownsHttpClient = ownsHttpClient;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    public async Task<TestResultUploadSummary> UploadTestResultsWithSummaryAsync(List<string> testResultFiles, object resultMetadata, CancellationToken cancellationToken = default)
    {
        long parseStartedAt = JobMonitorMetrics.StartOperation();
        bool parseRecorded = false;
        try
        {
            var testResultReader = new LocalTestResultsReader(_logger, _azdoParameters.TestResultAttachmentMode);

            var parsedResults = new List<IReadOnlyList<TestResult>>(testResultFiles.Count);
            foreach (string file in testResultFiles)
            {
                parsedResults.Add(await testResultReader.ReadResultFileAsync(file, cancellationToken));
            }

            if (parsedResults.Count == 0)
            {
                _logger.LogWarning("No test result files were provided for upload");
                return new TestResultUploadSummary(true, 0);
            }

            IReadOnlyList<AggregatedResult> aggregatedResults = new ResultAggregator().Aggregate(parsedResults, _azdoParameters.UseFullyQualifiedTestName);
            _metrics.RecordPipelineOperation(PipelineOperation.ResultParseAndAggregate, parseStartedAt);
            parseRecorded = true;
            if (aggregatedResults.Count == 0)
            {
                _logger.LogDebug("Test results were discovered but none could be aggregated");
                return new TestResultUploadSummary(true, 0);
            }

            long publishStartedAt = JobMonitorMetrics.StartOperation();
            long uploadedCount;
            try
            {
                uploadedCount = await UploadTestResultsWithCountAsync(
                    aggregatedResults,
                    resultMetadata,
                    cancellationToken);
            }
            finally
            {
                _metrics.RecordPipelineOperation(PipelineOperation.WorkItemPublish, publishStartedAt);
            }
            return new TestResultUploadSummary(
                AllPassed: ComputeAllPassed(aggregatedResults),
                UploadedCount: uploadedCount);
        }
        finally
        {
            if (!parseRecorded)
            {
                _metrics.RecordPipelineOperation(PipelineOperation.ResultParseAndAggregate, parseStartedAt);
            }
        }
    }

    /// <summary>
    /// A work item's uploaded results are only considered a failure when a test actually failed
    /// or could not be parsed into a known outcome ("None"). "Inconclusive" is a legitimate,
    /// non-failing outcome produced by the aggregator for data-driven tests that mix passing and
    /// skipped data rows (see <see cref="ResultAggregator"/>), so it must not fail the work item.
    /// </summary>
    internal static bool ComputeAllPassed(IReadOnlyList<AggregatedResult> results)
        => results.All(result => result.Result != "Failed" && result.Result != "None");

    public async Task<long> UploadTestResultsWithCountAsync(IEnumerable<AggregatedResult> results, object resultMetadata, CancellationToken cancellationToken = default)
    {
        try
        {
            long publishedTestCount = 0;
            foreach (List<ConvertedResult> requestBatch in CreateResultRequestBatches(ConvertResults(results, resultMetadata)))
            {
                IReadOnlyList<PublishedTestCase> publishedTests = await PublishResultsAsync(requestBatch, cancellationToken);
                publishedTestCount += publishedTests.Count;
            }

            _logger.LogDebug("Uploaded {Count} results", publishedTestCount);

            return publishedTestCount;
        }
        catch (TerminalError ex)
        {
            _logger.LogError(ex, "Failed to upload test results to Azure DevOps.");
            throw;
        }
    }

    private async Task<IReadOnlyList<PublishedTestCase>> PublishResultsAsync(
        IReadOnlyList<ConvertedResult> converted,
        CancellationToken cancellationToken)
    {
        var testCaseResults = converted.Select(static c => c.Converted).ToList();
        var originalList = converted.Select(static c => c.Aggregated).ToList();

        using HttpResponseMessage response = await SendWithRetryAsync(
            HttpMethod.Post,
            $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/results?api-version=7.1-preview.6",
            testCaseResults,
            DefaultAttemptCount,
            AzureDevOpsRequestKind.ResultBatch,
            cancellationToken);

        IReadOnlyList<PublishedTestCaseResultReference> publishedResults = await ReadPublishedResultsAsync(response, cancellationToken);
        if (publishedResults.Count == 0)
        {
            _logger.LogWarning("The test run appears to have been closed, aborting test result uploads.");
            return [];
        }

        List<PublishedTestCase> publishedTestCases = [];

        foreach ((PublishedTestCaseResultReference published, AggregatedResult original, PublishedTestCase testCase) in publishedResults.Zip(originalList, testCaseResults))
        {
            if (published.Id == -1)
            {
                _logger.LogWarning("Azure DevOps test ID returned -1, unable to attach files.");
                continue;
            }

            async Task IterateSubResultsAsync(
                IReadOnlyList<PublishedSubResultReference>? publishedSubResults,
                IReadOnlyList<AggregatedResult> originalSubResults,
                long testId)
            {
                if (publishedSubResults is null || publishedSubResults.Count == 0)
                {
                    if (originalSubResults.Count > 0)
                    {
                        _logger.LogError("Published results do not include sub-results, attachments lost.");
                    }

                    return;
                }

                if (publishedSubResults.Count != originalSubResults.Count)
                {
                    _logger.LogError("Published sub-result counts do not match uploaded attachments. Attachments lost.");
                    return;
                }

                foreach ((PublishedSubResultReference publishedSubResult, AggregatedResult originalSubResult) subTriplet in publishedSubResults.Zip(originalSubResults, (publishedSubResult, originalSubResult) => (publishedSubResult, originalSubResult)))
                {
                    foreach (TestResultAttachment attachment in subTriplet.originalSubResult.Attachments)
                    {
                        await SendAttachmentAsync(attachment, testId, subTriplet.publishedSubResult.Id, cancellationToken);
                    }

                    await IterateSubResultsAsync(subTriplet.publishedSubResult.SubResults, subTriplet.originalSubResult.SubResults, testId);
                }
            }

            foreach (TestResultAttachment attachment in original.Attachments)
            {
                await SendAttachmentAsync(attachment, published.Id, null, cancellationToken);
            }

            await IterateSubResultsAsync(published.SubResults, original.SubResults, published.Id);

            publishedTestCases.Add(testCase);
        }

        return publishedTestCases;
    }

    private async Task SendAttachmentAsync(
        TestResultAttachment attachment,
        long testId,
        long? subResultId,
        CancellationToken cancellationToken)
    {
        var request = new TestRunAttachmentRequest(
            attachment.Name,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(attachment.Text)));

        string path = subResultId is long subId
            ? $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/results/{testId}/attachments?testSubResultId={subId}&api-version=7.1-preview.1"
            : $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/results/{testId}/attachments?api-version=7.1-preview.1";

        using HttpResponseMessage response = await SendWithRetryAsync(
            HttpMethod.Post,
            path,
            request,
            DefaultAttemptCount,
            AzureDevOpsRequestKind.Attachment,
            cancellationToken);
        _ = response;
    }

    private IEnumerable<ConvertedResult> ConvertResults(IEnumerable<AggregatedResult> results, object resultMetadata)
    {
        static string GetResultGroupType(AggregationType aggregationType)
        {
            return aggregationType switch
            {
                AggregationType.Single => "None",
                AggregationType.DataDriven => "dataDriven",
                AggregationType.Rerun => "rerun",
                _ => "None",
            };
        }

        string comment = JsonSerializer.Serialize(resultMetadata) ?? string.Empty;
        bool useFullyQualifiedName = _azdoParameters.UseFullyQualifiedTestName;

        string DisplayNameFor(AggregatedResult result)
            => useFullyQualifiedName
                ? TestNameFormatter.FormatDisplayName(result.FullyQualifiedName, result.Name)
                : result.Name;

        PublishedSubResult ConvertToSubTest(AggregatedResult result)
        {
            var customFields = new List<CustomField>();
            if (result.IsFlaky)
            {
                customFields.Add(new CustomField("IsTestResultFlaky", true));
            }

            if ((result.AttemptId ?? 0) > 1)
            {
                customFields.Add(new CustomField("AttemptId", result.AttemptId!.Value - 1));
            }

            return new PublishedSubResult
            {
                Comment = comment,
                CustomFields = customFields,
                DisplayName = DisplayNameFor(result),
                Outcome = result.Result,
                DurationInMs = result.DurationSeconds * 1000.0,
                StackTrace = result.StackTrace,
                ErrorMessage = result.FailureMessage,
                SubResults = result.SubResults.Count == 0 ? null : [.. result.SubResults.Select(ConvertToSubTest)],
                ResultGroupType = GetResultGroupType(result.AggregationType),
            };
        }

        ConvertedResult ConvertResult(AggregatedResult result)
        {
            var customFields = new List<CustomField>();
            if (result.IsFlaky)
            {
                customFields.Add(new CustomField("IsTestResultFlaky", true));
            }

            if (result.AggregationType == AggregationType.Rerun && result.SubResults.Count > 1)
            {
                customFields.Add(new CustomField("AttemptId", result.SubResults.Count - 1));
            }

            string displayName = DisplayNameFor(result);

            return new ConvertedResult(
                new PublishedTestCase
                {
                    TestCaseTitle = displayName,
                    AutomatedTestName = useFullyQualifiedName ? result.FullyQualifiedName : result.Name,
                    AutomatedTestType = "helix",
                    AutomatedTestStorage = comment, // TODO: This was workitem ID
                    Priority = 1,
                    DurationInMs = result.DurationSeconds * 1000.0,
                    Outcome = result.Result,
                    State = "Completed",
                    Comment = comment,
                    StackTrace = result.StackTrace,
                    ErrorMessage = result.FailureMessage,
                    SubResults = result.SubResults.Count == 0 ? null : [.. result.SubResults.Select(ConvertToSubTest)],
                    ResultGroupType = GetResultGroupType(result.AggregationType),
                    CustomFields = customFields,
                },
                result);
        }

        foreach (AggregatedResult result in results)
        {
            foreach (ConvertedResult hierarchyPart in SplitOversizedResultHierarchy(
                ConvertResult(result),
                MaximumNodesPerResultHierarchy))
            {
                yield return hierarchyPart;
            }
        }
    }

    /// <summary>
    /// Groups converted top-level results into Azure DevOps request bodies. Each item counts
    /// once regardless of how many nested sub-results it contains.
    /// </summary>
    private static IEnumerable<List<ConvertedResult>> CreateResultRequestBatches(
        IEnumerable<ConvertedResult> results)
        => PartitionBySize(results, MaximumResultsPerRequest, static _ => 1);

    /// <summary>
    /// Splits one logical data-driven or rerun result into multiple top-level payload entries
    /// when its recursive hierarchy exceeds <paramref name="maximumNodesPerHierarchy"/>.
    /// This is separate from grouping top-level entries into Azure DevOps request batches.
    /// </summary>
    private static IEnumerable<ConvertedResult> SplitOversizedResultHierarchy(
        ConvertedResult test,
        int maximumNodesPerHierarchy)
    {
        if (CountResultTreeNodes(test.Converted) <= maximumNodesPerHierarchy)
        {
            yield return test;
            yield break;
        }

        if (maximumNodesPerHierarchy <= 1 || test.Converted.SubResults is not { Count: > 0 })
        {
            throw new InvalidOperationException(
                "A test-result hierarchy is deeper than the Azure DevOps hierarchy limit.");
        }

        IEnumerable<ChunkPair> splitSubTests = (test.Converted.SubResults ?? [])
            .Zip(test.Aggregated.SubResults, (converted, aggregated) => new ChunkPair(converted, aggregated))
            .SelectMany(pair => SplitOversizedSubResultHierarchy(pair, maximumNodesPerHierarchy - 1));

        // Each emitted hierarchy includes the copied top-level result, leaving the remaining
        // node budget for its sub-results.
        foreach (List<ChunkPair> hierarchyPart in PartitionBySize(
            splitSubTests,
            maximumNodesPerHierarchy - 1,
            static pair => CountResultTreeNodes(pair.Converted)))
        {
            yield return new ConvertedResult(
                test.Converted with { SubResults = [.. hierarchyPart.Select(static x => x.Converted)], Id = null },
                CopyAggregatedResult(
                    test.Aggregated,
                    [.. hierarchyPart.Select(static x => x.Aggregated)]));
        }
    }

    private static IEnumerable<ChunkPair> SplitOversizedSubResultHierarchy(
        ChunkPair test,
        int maximumNodesPerHierarchy)
    {
        if (CountResultTreeNodes(test.Converted) <= maximumNodesPerHierarchy)
        {
            yield return test;
            yield break;
        }

        IEnumerable<ChunkPair> splitSubTests = (test.Converted.SubResults ?? [])
            .Zip(test.Aggregated.SubResults, (converted, aggregated) => new ChunkPair(converted, aggregated))
            .SelectMany(pair => SplitOversizedSubResultHierarchy(pair, maximumNodesPerHierarchy - 1));

        foreach (List<ChunkPair> hierarchyPart in PartitionBySize(
            splitSubTests,
            maximumNodesPerHierarchy - 1,
            static pair => CountResultTreeNodes(pair.Converted)))
        {
            yield return new ChunkPair(
                test.Converted with
                {
                    SubResults = [.. hierarchyPart.Select(static x => x.Converted)],
                    Id = null,
                },
                CopyAggregatedResult(
                    test.Aggregated,
                    [.. hierarchyPart.Select(static x => x.Aggregated)]));
        }
    }

    private static AggregatedResult CopyAggregatedResult(
        AggregatedResult result,
        IReadOnlyList<AggregatedResult> subResults)
        => new(
            result.AggregationType,
            result.Name,
            result.DurationSeconds,
            result.Result,
            subResults,
            result.Attachments,
            result.FailureMessage,
            result.StackTrace,
            isFlaky: result.IsFlaky,
            attemptId: result.AttemptId,
            fullyQualifiedName: result.FullyQualifiedName);

    private static int CountResultTreeNodes(PublishedTestCase test)
    {
        return 1 + (test.SubResults?.Sum(CountResultTreeNodes) ?? 0);
    }

    private static int CountResultTreeNodes(PublishedSubResult test)
    {
        return 1 + (test.SubResults?.Sum(CountResultTreeNodes) ?? 0);
    }

    /// <summary>
    /// Partitions items in order so the sum of <paramref name="getSize"/> values in each
    /// partition does not exceed <paramref name="maximumPartitionSize"/>.
    /// </summary>
    private static IEnumerable<List<T>> PartitionBySize<T>(
        IEnumerable<T> items,
        int maximumPartitionSize,
        Func<T, int> getSize)
    {
        var currentPartition = new List<T>();
        int currentSize = 0;

        foreach (T? item in items)
        {
            int size = getSize(item);
            if (size > maximumPartitionSize)
            {
                throw new InvalidOperationException("Cannot partition an item larger than the size limit.");
            }

            if (currentSize + size > maximumPartitionSize && currentPartition.Count > 0)
            {
                yield return currentPartition;
                currentPartition = [];
                currentSize = 0;
            }

            currentPartition.Add(item);
            currentSize += size;
        }

        if (currentPartition.Count > 0)
        {
            yield return currentPartition;
        }
    }

    private static HttpClient CreateHttpClient(string? accessToken)
    {
        var client = new HttpClient { Timeout = s_httpClientTimeout };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            string basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{accessToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        }

        return client;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpMethod method,
        string relativePath,
        object? payload,
        int attemptCount,
        AzureDevOpsRequestKind requestKind,
        CancellationToken cancellationToken)
    {
        byte[]? body = payload is null ? null : JsonSerializer.SerializeToUtf8Bytes(payload, s_serializerOptions);

        HttpResponseMessage? successfulResponse = null;
        Exception? lastException = null;
        var retryHandler = new ExponentialRetry
        {
            MaxAttempts = attemptCount,
            DelayBase = 3,
            DelayConstant = 0,
            MinRandomFactor = 1,
            MaxRandomFactor = 1,
            MaximumDelay = s_maximumRetryDelay,
            RetryDelayCallback = (failedAttempt, delay) =>
                _logger.LogDebug(
                    "Azure DevOps {Method} request to '{RequestPath}' failed on attempt {Attempt} of {AttemptCount}. "
                    + "Waiting {RetryDelay} before the next attempt.",
                    method,
                    relativePath,
                    failedAttempt,
                    attemptCount,
                    delay),
        };

        bool succeeded = await retryHandler.RunAsync(
            async attempt =>
            {
                await _rateLimitGate.WaitAsync(cancellationToken);
                long requestStartedAt = JobMonitorMetrics.StartOperation();
                bool failed = true;

                Uri baseUri = _azdoParameters.CollectionUri.AbsoluteUri.EndsWith('/')
                    ? _azdoParameters.CollectionUri
                    : new Uri(_azdoParameters.CollectionUri.AbsoluteUri + '/', UriKind.Absolute);

                using var request = new HttpRequestMessage(method, new Uri(baseUri, relativePath));
                if (body is not null)
                {
                    request.Content = new ByteArrayContent(body);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }

                try
                {
                    DateTimeOffset logStartedAt = DateTimeOffset.UtcNow;
                    _logger.LogDebug(
                        "Sending Azure DevOps {Method} request to '{RequestPath}', attempt {Attempt} of {AttemptCount}.",
                        method,
                        relativePath,
                        attempt + 1,
                        attemptCount);
                    HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
                    if (response.IsSuccessStatusCode)
                    {
                        if (GetRateLimitDelay(response) is { } rateLimitDelay)
                        {
                            _rateLimitGate.Defer(rateLimitDelay);
                        }

                        _logger.LogDebug(
                            "Azure DevOps {Method} request to '{RequestPath}' completed with HTTP {StatusCode} "
                            + "on attempt {Attempt} of {AttemptCount} after {Elapsed}.",
                            method,
                            relativePath,
                            (int)response.StatusCode,
                            attempt + 1,
                            attemptCount,
                            DateTimeOffset.UtcNow - logStartedAt);
                        failed = false;
                        successfulResponse = response;
                        return RetryResult.Success;
                    }

                    using (response)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                        bool isTransientStatus = (int)response.StatusCode >= 500
                            || response.StatusCode == HttpStatusCode.TooManyRequests;
                        if (isTransientStatus)
                        {
                            TimeSpan? retryAfter = GetRateLimitDelay(response);
                            if (response.StatusCode == HttpStatusCode.TooManyRequests && retryAfter is null)
                            {
                                retryAfter = TimeSpan.FromSeconds(30);
                            }
                            if (retryAfter is { } delay)
                            {
                                _rateLimitGate.Defer(delay);
                            }

                            _logger.LogDebug(
                                "Azure DevOps {Method} request to '{RequestPath}' returned HTTP {StatusCode} "
                                + "on attempt {Attempt} of {AttemptCount} after {Elapsed}. Retrying.",
                                method,
                                relativePath,
                                (int)response.StatusCode,
                                attempt + 1,
                                attemptCount,
                                DateTimeOffset.UtcNow - logStartedAt);
                            lastException = new AzureDevOpsReportingError(
                                $"Azure DevOps request failed with status code {(int)response.StatusCode}: {responseBody}");
                            return RetryResult.Retry(retryAfter);
                        }

                        if (responseBody.Contains("It may have been deleted", StringComparison.OrdinalIgnoreCase)
                            || responseBody.Contains("not authorized to access this resource", StringComparison.OrdinalIgnoreCase)
                            || responseBody.Contains("cannot be added or updated for a test run which is in Completed state", StringComparison.OrdinalIgnoreCase)
                            || response.StatusCode == HttpStatusCode.Forbidden
                            || response.StatusCode == HttpStatusCode.Unauthorized)
                        {
                            throw new TerminalError(responseBody);
                        }

                        throw new AzureDevOpsReportingError(
                            $"Azure DevOps request failed with status code {(int)response.StatusCode}: {responseBody}");
                    }
                }
                catch (Exception ex) when (IsTransientException(ex, cancellationToken))
                {
                    lastException = ex;
                    _logger.LogDebug(
                        ex,
                        "Transient Azure DevOps {Method} request failure for '{RequestPath}' on attempt "
                        + "{Attempt} of {AttemptCount}. Retrying.",
                        method,
                        relativePath,
                        attempt + 1,
                        attemptCount);
                    return RetryResult.Retry();
                }
                finally
                {
                    _metrics.RecordAzureDevOpsRequest(
                        requestKind,
                        body?.Length ?? 0,
                        isRetry: attempt > 0,
                        failed: failed,
                        startedAt: requestStartedAt);
                }
            },
            cancellationToken);

        return succeeded && successfulResponse is not null
            ? successfulResponse
            : throw lastException ?? new InvalidOperationException("Azure DevOps retry loop exited unexpectedly.");
    }

    internal static bool IsTransientException(Exception exception, CancellationToken cancellationToken)
        => !cancellationToken.IsCancellationRequested
            && exception is OperationCanceledException { InnerException: TimeoutException }
                or HttpRequestException
            or TimeoutException
            or SocketException
            or IOException;

    internal static TimeSpan? GetRateLimitDelay(HttpResponseMessage response)
    {
        TimeSpan delay = TimeSpan.Zero;
        RetryConditionHeaderValue? retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > delay)
        {
            delay = delta;
        }

        if (retryAfter?.Date is { } date)
        {
            TimeSpan datedDelay = date - DateTimeOffset.UtcNow;
            if (datedDelay > delay)
            {
                delay = datedDelay;
            }
        }

        if (response.Headers.TryGetValues("X-RateLimit-Delay", out IEnumerable<string>? delayValues)
            && double.TryParse(
                delayValues.FirstOrDefault(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double delaySeconds)
            && delaySeconds > 0)
        {
            TimeSpan headerDelay = TimeSpan.FromSeconds(delaySeconds);
            if (headerDelay > delay)
            {
                delay = headerDelay;
            }
        }

        return delay > TimeSpan.Zero ? delay : null;
    }

    private static async Task<IReadOnlyList<PublishedTestCaseResultReference>> ReadPublishedResultsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        using var document = JsonDocument.Parse(content);
        JsonElement root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return [.. root.EnumerateArray().Select(ParsePublishedResult)];
        }

        if (root.TryGetProperty("value", out JsonElement value) && value.ValueKind == JsonValueKind.Array)
        {
            return [.. value.EnumerateArray().Select(ParsePublishedResult)];
        }

        return [];
    }

    private static PublishedTestCaseResultReference ParsePublishedResult(JsonElement element)
    {
        var subResults = new List<PublishedSubResultReference>();
        if (element.TryGetProperty("subResults", out JsonElement subResultElement) && subResultElement.ValueKind == JsonValueKind.Array)
        {
            subResults.AddRange(subResultElement.EnumerateArray().Select(ParsePublishedSubResult));
        }

        return new PublishedTestCaseResultReference(
            element.TryGetProperty("id", out JsonElement idElement) ? idElement.GetInt64() : -1,
            subResults);
    }

    private static PublishedSubResultReference ParsePublishedSubResult(JsonElement element)
    {
        var subResults = new List<PublishedSubResultReference>();
        if (element.TryGetProperty("subResults", out JsonElement subResultElement) && subResultElement.ValueKind == JsonValueKind.Array)
        {
            subResults.AddRange(subResultElement.EnumerateArray().Select(ParsePublishedSubResult));
        }

        return new PublishedSubResultReference(
            element.TryGetProperty("id", out JsonElement idElement) ? idElement.GetInt64() : -1,
            subResults);
    }

    private sealed record ConvertedResult(PublishedTestCase Converted, AggregatedResult Aggregated);

    private sealed record ChunkPair(PublishedSubResult Converted, AggregatedResult Aggregated);

    private sealed record TestRunAttachmentRequest(string FileName, string Stream);

    private sealed record CustomField(string FieldName, object Value);

    private sealed record PublishedTestCase
    {
        public long? Id { get; init; }

        public string TestCaseTitle { get; init; } = string.Empty;

        public string AutomatedTestName { get; init; } = string.Empty;

        public string AutomatedTestType { get; init; } = string.Empty;

        public string AutomatedTestStorage { get; init; } = string.Empty;

        public int Priority { get; init; }

        public double DurationInMs { get; init; }

        public string Outcome { get; init; } = string.Empty;

        public string State { get; init; } = string.Empty;

        public string Comment { get; init; } = string.Empty;

        public string? StackTrace { get; init; }

        public string? ErrorMessage { get; init; }

        public List<PublishedSubResult>? SubResults { get; init; }

        public string ResultGroupType { get; init; } = string.Empty;

        public List<CustomField>? CustomFields { get; init; }
    }

    private sealed record PublishedSubResult
    {
        public long? Id { get; init; }

        public string Comment { get; init; } = string.Empty;

        public List<CustomField>? CustomFields { get; init; }

        public string DisplayName { get; init; } = string.Empty;

        public string Outcome { get; init; } = string.Empty;

        public double DurationInMs { get; init; }

        public string? StackTrace { get; init; }

        public string? ErrorMessage { get; init; }

        public List<PublishedSubResult>? SubResults { get; init; }

        public string ResultGroupType { get; init; } = string.Empty;
    }

    private sealed record PublishedTestCaseResultReference(long Id, IReadOnlyList<PublishedSubResultReference> SubResults);

    private sealed record PublishedSubResultReference(long Id, IReadOnlyList<PublishedSubResultReference> SubResults);
}
