// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Helix.AzureDevOpsTestReporter.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestReporter;

public interface ITestReporter
{
    Task ReportResultsAsync(IReadOnlyList<TestResult> results, CancellationToken cancellationToken = default);
}

public sealed record AzureDevOpsReportingParameters(Uri CollectionUri, string TeamProject, string TestRunId);

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
