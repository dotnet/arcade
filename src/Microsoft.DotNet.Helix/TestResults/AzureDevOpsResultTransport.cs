// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using Microsoft.Arcade.Common;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

internal sealed class AzureDevOpsResultTransport : IAzureDevOpsResultTransport
{
    private const int ControlRequestAttemptCount = 5;
    private const int ResultRequestAttemptCount = 10;
    private static readonly System.Text.Json.JsonSerializerOptions s_serializerOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);
    private readonly string _projectUri;
    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly TestReportingMetrics _metrics;
    private readonly AzureDevOpsRateLimitGate _rateLimitGate;

    public AzureDevOpsResultTransport(
        string collectionUri,
        string teamProject,
        HttpClient client,
        ILogger logger,
        TestReportingMetrics metrics = null)
    {
        _projectUri = $"{collectionUri.TrimEnd('/')}/{Uri.EscapeDataString(teamProject)}";
        _client = client;
        _logger = logger;
        _metrics = metrics ?? new TestReportingMetrics();
        _rateLimitGate = new AzureDevOpsRateLimitGate(_metrics);
    }

    public Task<string> PublishResultsAsync(
        int testRunId,
        object results,
        CancellationToken cancellationToken)
        => SendForStringAsync(
            HttpMethod.Post,
            $"{_projectUri}/_apis/test/runs/{testRunId}/results?api-version=7.1-preview.6",
            System.Text.Json.JsonSerializer.Serialize(results, s_serializerOptions),
            AzureDevOpsRequestKind.ResultBatch,
            retryTransientFailures: true,
            ResultRequestAttemptCount,
            cancellationToken);

    public Task UploadAttachmentAsync(
        int testRunId,
        long testResultId,
        long? testSubResultId,
        string fileName,
        string stream,
        CancellationToken cancellationToken)
    {
        string query = testSubResultId is long subResultId
            ? $"?testSubResultId={subResultId}&api-version=7.1-preview.1"
            : "?api-version=7.1-preview.1";
        var body = new JObject { ["fileName"] = fileName, ["stream"] = stream };
        return SendForStringAsync(
            HttpMethod.Post,
            $"{_projectUri}/_apis/test/runs/{testRunId}/results/{testResultId}/attachments{query}",
            body.ToString(Formatting.None),
            AzureDevOpsRequestKind.Attachment,
            retryTransientFailures: true,
            ResultRequestAttemptCount,
            cancellationToken);
    }

    internal async Task<JObject> SendAsync(
        HttpMethod method,
        string requestUri,
        JToken body = null,
        bool retryTransientFailures = true,
        CancellationToken cancellationToken = default)
    {
        string content = await SendForStringAsync(method, requestUri, body, retryTransientFailures, cancellationToken);
        return string.IsNullOrWhiteSpace(content) ? [] : JObject.Parse(content);
    }

    internal Task<string> SendForStringAsync(
        HttpMethod method,
        string requestUri,
        JToken body = null,
        bool retryTransientFailures = true,
        CancellationToken cancellationToken = default)
        => SendForStringAsync(
            method,
            requestUri,
            body?.ToString(Formatting.None),
            AzureDevOpsRequestKind.Control,
            retryTransientFailures,
            ControlRequestAttemptCount,
            cancellationToken);

    private async Task<string> SendForStringAsync(
        HttpMethod method,
        string requestUri,
        string serializedBody,
        AzureDevOpsRequestKind requestKind,
        bool retryTransientFailures,
        int attemptCount,
        CancellationToken cancellationToken)
    {
        int payloadBytes = serializedBody is null ? 0 : Encoding.UTF8.GetByteCount(serializedBody);

        async Task<string> SendOnceAsync(int attempt)
        {
            await _rateLimitGate.WaitAsync(cancellationToken);
            long requestStartedAt = TestReportingMetrics.StartOperation();
            bool failed = true;
            using var request = new HttpRequestMessage(method, requestUri);
            if (serializedBody != null)
            {
                request.Content = new StringContent(serializedBody, Encoding.UTF8, "application/json");
            }

            try
            {
                using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
                string content = await response.Content.ReadAsStringAsync(cancellationToken);
                TimeSpan? rateLimitDelay = GetRateLimitDelay(response);
                if (response.StatusCode == HttpStatusCode.TooManyRequests && rateLimitDelay is null)
                {
                    rateLimitDelay = TimeSpan.FromSeconds(30);
                }

                if (rateLimitDelay is { } delay)
                {
                    _rateLimitGate.ExtendDeadline(delay);
                }

                if (!response.IsSuccessStatusCode)
                {
                    ThrowForFailure(response, content, requestUri, requestKind, rateLimitDelay);
                }

                failed = false;
                return content;
            }
            finally
            {
                _metrics.RecordAzureDevOpsRequest(
                    requestKind, payloadBytes, isRetry: attempt > 0, failed, requestStartedAt);
            }
        }

        if (!retryTransientFailures)
        {
            return await SendOnceAsync(0);
        }

        string result = null;
        Exception lastException = null;
        var retryHandler = new ExponentialRetry
        {
            MaxAttempts = attemptCount,
            DelayBase = requestKind == AzureDevOpsRequestKind.Control ? 2 : 3,
            DelayConstant = 0,
            MinRandomFactor = 1,
            MaxRandomFactor = 1,
            MaximumDelay = TimeSpan.FromSeconds(30),
            RetryDelayCallback = (failedAttempt, delay) =>
                _logger.LogDebug(
                    "Azure DevOps {Method} request to '{RequestUri}' failed on attempt {Attempt} of {AttemptCount}. "
                    + "Waiting {RetryDelay} before the next attempt.",
                    method, requestUri, failedAttempt, attemptCount, delay),
        };

        bool succeeded = await retryHandler.RunAsync(
            async attempt =>
            {
                try
                {
                    result = await SendOnceAsync(attempt);
                    return RetryResult.Success;
                }
                catch (Exception ex) when (IsTransientException(ex, cancellationToken))
                {
                    lastException = ex;
                    return RetryResult.Retry((ex as TransientAzureDevOpsRequestException)?.RetryAfter);
                }
            },
            cancellationToken);

        return succeeded
            ? result
            : throw lastException ?? new InvalidOperationException("Retry failed without completing the Azure DevOps request.");
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
        RetryConditionHeaderValue retryAfterHeader = response.Headers.RetryAfter;
        if (retryAfterHeader?.Delta is { } delta && delta > delay)
        {
            delay = delta;
        }
        if (retryAfterHeader?.Date is { } date)
        {
            TimeSpan datedDelay = TimeSpan.FromTicks(date.UtcTicks - DateTimeOffset.UtcNow.UtcTicks);
            if (datedDelay > delay)
            {
                delay = datedDelay;
            }
        }
        if (response.Headers.TryGetValues("X-RateLimit-Delay", out IEnumerable<string> delayValues) &&
            double.TryParse(delayValues.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out double delaySeconds) &&
            delaySeconds > 0)
        {
            TimeSpan rateLimitDelay = TimeSpan.FromSeconds(delaySeconds);
            if (rateLimitDelay > delay)
            {
                delay = rateLimitDelay;
            }
        }
        return delay > TimeSpan.Zero ? delay : null;
    }

    private static void ThrowForFailure(
        HttpResponseMessage response,
        string responseBody,
        string requestUri,
        AzureDevOpsRequestKind requestKind,
        TimeSpan? rateLimitDelay)
    {
        if (responseBody.Contains("It may have been deleted", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("not authorized to access this resource", StringComparison.OrdinalIgnoreCase)
            || responseBody.Contains("cannot be added or updated for a test run which is in Completed state", StringComparison.OrdinalIgnoreCase)
            || response.StatusCode == HttpStatusCode.Forbidden
            || response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new TerminalError(responseBody);
        }

        string message = $"Request to {requestUri} failed with {(int)response.StatusCode} {response.ReasonPhrase}. {responseBody}";
        if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new TransientAzureDevOpsRequestException(
                message, response.StatusCode,
                rateLimitDelay ?? (response.StatusCode == HttpStatusCode.TooManyRequests ? TimeSpan.FromSeconds(30) : null));
        }
        if (requestKind == AzureDevOpsRequestKind.Control)
        {
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        throw new AzureDevOpsReportingError(message);
    }

    private sealed class TransientAzureDevOpsRequestException(
        string message,
        HttpStatusCode statusCode,
        TimeSpan? retryAfter) : HttpRequestException(message, null, statusCode)
    {
        public TimeSpan? RetryAfter { get; } = retryAfter;
    }
}
