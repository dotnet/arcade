// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestReporter;

public sealed record TestResultAttachment(string Name, string Text);

public sealed class TestResult
{
    public TestResult(
        string name,
        string kind,
        string typeName,
        string method,
        double durationSeconds,
        string result,
        string? exceptionType,
        string? failureMessage,
        string? stackTrace,
        string? skipReason,
        IReadOnlyList<TestResultAttachment>? attachments = null)
    {
        Name = name ?? string.Empty;
        Kind = kind ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        Method = method ?? string.Empty;
        DurationSeconds = durationSeconds;
        Result = result ?? string.Empty;
        ExceptionType = exceptionType;
        FailureMessage = failureMessage;
        StackTrace = stackTrace;
        SkipReason = skipReason;
        Attachments = attachments ?? Array.Empty<TestResultAttachment>();
    }

    public string Name { get; }

    public string Kind { get; }

    public string TypeName { get; }

    public string Method { get; }

    public double DurationSeconds { get; }

    public string Result { get; }

    public string? ExceptionType { get; }

    public string? FailureMessage { get; }

    public string? StackTrace { get; }

    public string? SkipReason { get; }

    public IReadOnlyList<TestResultAttachment> Attachments { get; }

    public bool Ignored { get; set; }
}

public interface ITestReporter
{
    Task ReportResultsAsync(IReadOnlyList<TestResult> results, CancellationToken cancellationToken = default);
}

public sealed record AzureDevOpsReportingParameters(Uri CollectionUri, string TeamProject, string TestRunId, string AccessToken);

public sealed record PackedTestReport(AzureDevOpsReportingParameters AzdoParameters, IReadOnlyList<TestResult> Results);

public sealed class HelixEnvironmentSettings
{
    public string? CorrelationId { get; init; }

    public string? WorkItemId { get; init; }

    public string? WorkItemFriendlyName { get; init; }

    public string? WorkitemWorkingDir { get; init; }

    public string? WorkitemPayloadDir { get; init; }

    public static HelixEnvironmentSettings FromEnvironment()
    {
        return new HelixEnvironmentSettings
        {
            CorrelationId = Environment.GetEnvironmentVariable("HELIX_CORRELATION_ID"),
            WorkItemId = Environment.GetEnvironmentVariable("HELIX_WORKITEM_ID"),
            WorkItemFriendlyName = Environment.GetEnvironmentVariable("HELIX_WORKITEM_FRIENDLYNAME"),
            WorkitemWorkingDir = Environment.GetEnvironmentVariable("HELIX_WORKITEM_ROOT"),
            WorkitemPayloadDir = Environment.GetEnvironmentVariable("HELIX_WORKITEM_PAYLOAD"),
        };
    }
}

public interface IEventClient
{
    Task SendAsync(object payload, CancellationToken cancellationToken = default);

    Task ErrorAsync(
        HelixEnvironmentSettings settings,
        string errorType,
        string message,
        string? logUri = null,
        CancellationToken cancellationToken = default);
}

public interface IUploadClient
{
    Task<string> UploadAsync(
        Stream file,
        string name,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default);

    Task<string> UploadAsync(
        ReadOnlyMemory<byte> fileBytes,
        string name,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default);
}

public sealed class NullEventClient : IEventClient
{
    public static readonly NullEventClient Instance = new();

    private NullEventClient()
    {
    }

    public Task SendAsync(object payload, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task ErrorAsync(
        HelixEnvironmentSettings settings,
        string errorType,
        string message,
        string? logUri = null,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

public sealed class NullUploadClient : IUploadClient
{
    public static readonly NullUploadClient Instance = new();

    private NullUploadClient()
    {
    }

    public Task<string> UploadAsync(
        Stream file,
        string name,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"memory://{name}");
    }

    public Task<string> UploadAsync(
        ReadOnlyMemory<byte> fileBytes,
        string name,
        string contentType = "application/octet-stream",
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"memory://{name}");
    }
}

public static class LoggerFactoryExtensions
{
    public static ILogger OrNull(this ILogger? logger)
    {
        return logger ?? NullLogger.Instance;
    }
}
