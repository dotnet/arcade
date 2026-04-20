// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestReporter;

public enum UploadResult
{
    Success = 1,
    UnknownError = 2,
    TerminalError = 3,
}

public static class UploadResultExtensions
{
    public static UploadResult Aggregate(this UploadResult value, UploadResult other)
    {
        return (UploadResult)Math.Max((int)value, (int)other);
    }
}

public sealed class AzureDevOpsReportingError : Exception
{
    public AzureDevOpsReportingError(string message)
        : base(message)
    {
    }
}

internal sealed class TerminalError : Exception
{
    public TerminalError(string message)
        : base(message)
    {
    }
}

public sealed class AzureDevOpsResultPublisher
{
    private const int TestListBuckets = 32;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
    };

    private static string s_lastSendContent = string.Empty;

    private readonly AzureDevOpsReportingParameters _azdoParameters;
    private readonly string _workItemId;
    private readonly IEventClient _eventClient;
    private readonly IUploadClient _uploadClient;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public AzureDevOpsResultPublisher(
        AzureDevOpsReportingParameters azdoParameters,
        string workItemId,
        IEventClient? eventClient = null,
        IUploadClient? uploadClient = null,
        HttpClient? httpClient = null,
        ILogger? logger = null)
    {
        _azdoParameters = azdoParameters;
        _workItemId = workItemId;
        _eventClient = eventClient ?? NullEventClient.Instance;
        _uploadClient = uploadClient ?? NullUploadClient.Instance;
        _httpClient = httpClient ?? CreateHttpClient(azdoParameters.AccessToken);
        _logger = logger.OrNull();
    }

    public async Task<UploadResult> TryUploadAsync(IEnumerable<AggregatedResult> results, CancellationToken cancellationToken = default)
    {
        try
        {
            await ProcessAsync(results.ToList(), cancellationToken);
            return UploadResult.Success;
        }
        catch (TerminalError ex)
        {
            await LogErrorAsync(ex, cancellationToken);
            return UploadResult.TerminalError;
        }
        catch (Exception ex)
        {
            await LogErrorAsync(ex, cancellationToken);
            return UploadResult.UnknownError;
        }
    }

    private async Task ProcessAsync(IReadOnlyList<AggregatedResult> testList, CancellationToken cancellationToken)
    {
        var converted = ConvertResults(testList).ToList();
        var hotPathTests = new List<PublishedTestCase>();

        foreach (var batch in Batch(converted, 1000, static t => Size(t.Converted)))
        {
            var publishedTests = await PublishResultsAsync(batch, cancellationToken);
            hotPathTests.AddRange(publishedTests);
            _logger.LogInformation("Uploaded {Count} results", publishedTests.Count);
        }

        await SendMetadataAsync(hotPathTests, testList, cancellationToken);
    }

    private async Task LogErrorAsync(Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Failed to upload test results to Azure DevOps.");
        await _eventClient.ErrorAsync(
            HelixEnvironmentSettings.FromEnvironment(),
            "DevOpsReportFailure",
            $"Failed to upload results: {exception.Message}",
            cancellationToken: cancellationToken);
    }

    private async Task SendMetadataAsync(
        IReadOnlyList<PublishedTestCase> backChannelCases,
        IEnumerable<AggregatedResult> allTestResults,
        CancellationToken cancellationToken)
    {
        var partitionedResults = new Dictionary<int, List<TestListRow>>();
        var resultCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        void ProcessResultForMetadata(AggregatedResult result)
        {
            resultCounts[result.Result] = resultCounts.TryGetValue(result.Result, out var count) ? count + 1 : 1;
            if (!string.Equals(result.Result, "Passed", StringComparison.Ordinal))
            {
                return;
            }

            var name = result.Name;
            string? argumentHash = null;
            var partitionKey = name;
            var parenthesisIndex = name.IndexOf('(');
            if (parenthesisIndex >= 0)
            {
                var argumentList = name[(parenthesisIndex + 1)..].TrimEnd(')');
                name = name[..parenthesisIndex];
                argumentHash = Convert.ToBase64String(SHA1.HashData(Encoding.UTF8.GetBytes(argumentList)));
                partitionKey = name + argumentHash;
            }

            var bucket = SHA1.HashData(Encoding.UTF8.GetBytes(partitionKey))[0] % TestListBuckets;
            if (!partitionedResults.TryGetValue(bucket, out var testNames))
            {
                testNames = new List<TestListRow>();
                partitionedResults[bucket] = testNames;
            }

            testNames.Add(new TestListRow(name, argumentHash));
        }

        void ProcessTestForMetadata(AggregatedResult result)
        {
            if (result.AggregationType == AggregationType.DataDriven && result.SubResults.Count > 0)
            {
                foreach (var subResult in result.SubResults)
                {
                    ProcessTestForMetadata(subResult);
                }
            }
            else if (result.AggregationType == AggregationType.Single)
            {
                ProcessResultForMetadata(result);
            }
        }

        foreach (var result in allTestResults)
        {
            ProcessTestForMetadata(result);
        }

        var uploadedUrls = new Dictionary<int, string>();
        foreach (var (key, testNames) in partitionedResults)
        {
            var csvBytes = CreateCompressedCsv(testNames);
            var fileName = $"{Guid.NewGuid():N}.csv.gz";
            uploadedUrls[key] = await _uploadClient.UploadAsync(csvBytes, fileName, "application/gzip", cancellationToken);
        }

        var dataModel = new
        {
            version = 2,
            rerun_tests = backChannelCases,
            test_lists = uploadedUrls,
            partitions = TestListBuckets,
            result_counts = resultCounts,
        };

        var rawBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(dataModel, SerializerOptions));
        var compressedBytes = Compress(rawBytes);
        var base64Data = Convert.ToBase64String(compressedBytes);
        var fileNameBase = $"__helix_metadata_{Guid.NewGuid():N}.json.gz";

        await SendWithRetryAsync(
            HttpMethod.Post,
            $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/attachments?api-version=7.1-preview.1",
            new TestRunAttachmentRequest(fileNameBase, base64Data),
            cancellationToken);

        var metadataUrl = await _uploadClient.UploadAsync(compressedBytes, fileNameBase, "application/gzip", cancellationToken);
        await _eventClient.SendAsync(
            new
            {
                Type = "AzureDevOpsTestRunMetadata",
                TestRunProject = _azdoParameters.TeamProject,
                TestRunId = _azdoParameters.TestRunId,
                Url = metadataUrl,
            },
            cancellationToken);
    }

    private async Task<IReadOnlyList<PublishedTestCase>> PublishResultsAsync(
        IReadOnlyList<ConvertedResult> converted,
        CancellationToken cancellationToken)
    {
        var testCaseResults = converted.Select(static c => c.Converted).ToList();
        var originalList = converted.Select(static c => c.Aggregated).ToList();

        using var response = await SendWithRetryAsync(
            HttpMethod.Post,
            $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/results?api-version=7.1-preview.6",
            testCaseResults,
            cancellationToken);

        var publishedResults = await ReadPublishedResultsAsync(response, cancellationToken);
        if (publishedResults.Count == 0)
        {
            _logger.LogWarning("The test run appears to have been closed, aborting test result uploads.");
            return Array.Empty<PublishedTestCase>();
        }

        var hotPathTests = new List<PublishedTestCase>();
        foreach (var triplet in publishedResults.Zip(originalList, testCaseResults))
        {
            var published = triplet.First;
            var original = triplet.Second;
            var testCase = triplet.Third;

            if (published.Id == -1)
            {
                _logger.LogWarning("Azure DevOps test ID returned -1, unable to attach files.");
                continue;
            }

            testCase = testCase with { Id = published.Id };
            var addedTest = false;

            void AddToHotPath()
            {
                if (addedTest)
                {
                    return;
                }

                addedTest = true;
                hotPathTests.Add(testCase);
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

                if (original.AggregationType == AggregationType.Rerun)
                {
                    AddToHotPath();
                }

                if (publishedSubResults.Count != originalSubResults.Count)
                {
                    _logger.LogError("Published sub-result counts do not match uploaded attachments. Attachments lost.");
                    return;
                }

                foreach (var subTriplet in publishedSubResults.Zip(originalSubResults, (publishedSubResult, originalSubResult) => (publishedSubResult, originalSubResult)))
                {
                    foreach (var attachment in subTriplet.originalSubResult.Attachments)
                    {
                        await SendAttachmentAsync(attachment, testId, subTriplet.publishedSubResult.Id, cancellationToken);
                    }

                    await IterateSubResultsAsync(subTriplet.publishedSubResult.SubResults, subTriplet.originalSubResult.SubResults, testId);
                }
            }

            foreach (var attachment in original.Attachments)
            {
                await SendAttachmentAsync(attachment, published.Id, null, cancellationToken);
            }

            await IterateSubResultsAsync(published.SubResults, original.SubResults, published.Id);
        }

        return hotPathTests;
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

        var path = subResultId is long subId
            ? $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/results/{testId}/subresults/{subId}/attachments?api-version=7.1-preview.1"
            : $"{_azdoParameters.TeamProject}/_apis/test/runs/{_azdoParameters.TestRunId}/results/{testId}/attachments?api-version=7.1-preview.1";

        using var response = await SendWithRetryAsync(HttpMethod.Post, path, request, cancellationToken);
        _ = response;
    }

    private IEnumerable<ConvertedResult> ConvertResults(IEnumerable<AggregatedResult> results)
    {
        var settings = HelixEnvironmentSettings.FromEnvironment();
        var comment = JsonSerializer.Serialize(new
        {
            HelixJobId = settings.CorrelationId,
            HelixWorkItemName = settings.WorkItemFriendlyName,
        });

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
                Comment = comment ?? string.Empty,
                CustomFields = customFields,
                DisplayName = result.Name,
                Outcome = result.Result,
                DurationInMs = result.DurationSeconds * 1000.0,
                StackTrace = result.StackTrace,
                ErrorMessage = result.FailureMessage,
                SubResults = result.SubResults.Count == 0 ? null : result.SubResults.Select(ConvertToSubTest).ToList(),
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

            return new ConvertedResult(
                new PublishedTestCase
                {
                    TestCaseTitle = result.Name,
                    AutomatedTestName = result.Name,
                    AutomatedTestType = "helix",
                    AutomatedTestStorage = _workItemId,
                    Priority = 1,
                    DurationInMs = result.DurationSeconds * 1000.0,
                    Outcome = result.Result,
                    State = "Completed",
                    Comment = comment ?? string.Empty,
                    StackTrace = result.StackTrace,
                    ErrorMessage = result.FailureMessage,
                    SubResults = result.SubResults.Count == 0 ? null : result.SubResults.Select(ConvertToSubTest).ToList(),
                    ResultGroupType = GetResultGroupType(result.AggregationType),
                    CustomFields = customFields,
                },
                result);
        }

        var converted = results.Select(ConvertResult).ToList();
        foreach (var result in converted)
        {
            foreach (var chunk in Chunk(result, 950))
            {
                yield return chunk;
            }
        }
    }

    private static IEnumerable<ConvertedResult> Chunk(ConvertedResult test, int limit)
    {
        if (Size(test.Converted) <= limit)
        {
            yield return test;
            yield break;
        }

        var zippedSubTests = (test.Converted.SubResults ?? new List<PublishedSubResult>())
            .Zip(test.Aggregated.SubResults, (converted, aggregated) => new ChunkPair(converted, aggregated));

        foreach (var zippedBatch in Batch(zippedSubTests, limit, static pair => Size(pair.Converted)))
        {
            yield return new ConvertedResult(
                test.Converted with { SubResults = zippedBatch.Select(static x => x.Converted).ToList(), Id = null },
                new AggregatedResult(
                    test.Aggregated.AggregationType,
                    test.Aggregated.Name,
                    test.Aggregated.DurationSeconds,
                    test.Aggregated.Result,
                    zippedBatch.Select(static x => x.Aggregated).ToList(),
                    test.Aggregated.Attachments,
                    test.Aggregated.FailureMessage,
                    test.Aggregated.StackTrace,
                    isFlaky: test.Aggregated.IsFlaky,
                    attemptId: test.Aggregated.AttemptId));
        }
    }

    private static int Size(PublishedTestCase test)
    {
        return 1 + (test.SubResults?.Sum(Size) ?? 0);
    }

    private static int Size(PublishedSubResult test)
    {
        return 1 + (test.SubResults?.Sum(Size) ?? 0);
    }

    private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> items, int limit, Func<T, int> getSize)
    {
        var currentBatch = new List<T>();
        var currentSize = 0;

        foreach (var item in items)
        {
            var size = getSize(item);
            if (size > limit)
            {
                throw new InvalidOperationException("Cannot split a result larger than the batching limit.");
            }

            if (currentSize + size > limit && currentBatch.Count > 0)
            {
                yield return currentBatch;
                currentBatch = new List<T>();
                currentSize = 0;
            }

            currentBatch.Add(item);
            currentSize += size;
        }

        if (currentBatch.Count > 0)
        {
            yield return currentBatch;
        }
    }

    private static HttpClient CreateHttpClient(string accessToken)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            var basicToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{accessToken}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicToken);
        }

        return client;
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpMethod method,
        string relativePath,
        object? payload,
        CancellationToken cancellationToken)
    {
        var triesLeft = 10;
        var body = payload is null ? null : JsonSerializer.Serialize(payload, SerializerOptions);
        if (!string.IsNullOrEmpty(body))
        {
            s_lastSendContent = body;
        }

        while (true)
        {
            using var request = new HttpRequestMessage(method, new Uri(_azdoParameters.CollectionUri, relativePath));
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode == HttpStatusCode.ServiceUnavailable && triesLeft > 0)
            {
                response.Dispose();
                triesLeft--;
                _logger.LogWarning("Hit HTTP 503 from Azure DevOps. Waiting three seconds and trying again.");
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                continue;
            }

            if (responseBody.Contains("It may have been deleted", StringComparison.OrdinalIgnoreCase)
                || responseBody.Contains("not authorized to access this resource", StringComparison.OrdinalIgnoreCase)
                || responseBody.Contains("cannot be added or updated for a test run which is in Completed state", StringComparison.OrdinalIgnoreCase)
                || response.StatusCode == HttpStatusCode.Forbidden
                || response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new TerminalError(responseBody);
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(s_lastSendContent))
                {
                    await _uploadClient.UploadAsync(
                        Encoding.UTF8.GetBytes(s_lastSendContent),
                        "__failed_azdo_request_content.json",
                        "text/plain; charset=UTF-8",
                        cancellationToken);
                }
            }
            catch (Exception uploadException)
            {
                _logger.LogError(uploadException, "Failed to upload failed request payload.");
            }

            response.Dispose();
            throw new AzureDevOpsReportingError($"Azure DevOps request failed with status code {(int)response.StatusCode}: {responseBody}");
        }
    }

    private static async Task<IReadOnlyList<PublishedTestCaseResultReference>> ReadPublishedResultsAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<PublishedTestCaseResultReference>();
        }

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray().Select(ParsePublishedResult).ToList();
        }

        if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Select(ParsePublishedResult).ToList();
        }

        return Array.Empty<PublishedTestCaseResultReference>();
    }

    private static PublishedTestCaseResultReference ParsePublishedResult(JsonElement element)
    {
        var subResults = new List<PublishedSubResultReference>();
        if (element.TryGetProperty("subResults", out var subResultElement) && subResultElement.ValueKind == JsonValueKind.Array)
        {
            subResults.AddRange(subResultElement.EnumerateArray().Select(ParsePublishedSubResult));
        }

        return new PublishedTestCaseResultReference(
            element.TryGetProperty("id", out var idElement) ? idElement.GetInt64() : -1,
            subResults);
    }

    private static PublishedSubResultReference ParsePublishedSubResult(JsonElement element)
    {
        var subResults = new List<PublishedSubResultReference>();
        if (element.TryGetProperty("subResults", out var subResultElement) && subResultElement.ValueKind == JsonValueKind.Array)
        {
            subResults.AddRange(subResultElement.EnumerateArray().Select(ParsePublishedSubResult));
        }

        return new PublishedSubResultReference(
            element.TryGetProperty("id", out var idElement) ? idElement.GetInt64() : -1,
            subResults);
    }

    private static byte[] CreateCompressedCsv(IEnumerable<TestListRow> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.Append(EscapeCsv(row.TestName));
            builder.Append(',');
            builder.Append(EscapeCsv(row.ArgumentHash));
            builder.AppendLine();
        }

        return Compress(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static byte[] Compress(ReadOnlySpan<byte> rawBytes)
    {
        using var target = new MemoryStream();
        using (var gzip = new GZipStream(target, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(rawBytes);
        }

        return target.ToArray();
    }

    private sealed record ConvertedResult(PublishedTestCase Converted, AggregatedResult Aggregated);

    private sealed record ChunkPair(PublishedSubResult Converted, AggregatedResult Aggregated);

    private sealed record TestListRow(string TestName, string? ArgumentHash);

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
