// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.DotNet.Helix.AzureDevOpsTestPublisher.Model;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

public interface ITestReporter
{
    Task ReportResultsAsync(IReadOnlyList<TestResult> results, CancellationToken cancellationToken = default);
}

public sealed class PackingTestReporter(AzureDevOpsReportingParameters azdoParameters, ILogger? logger = null)
    : ITestReporter
{
    private const string ReportFileName = "__test_report.json";
    private static readonly JsonSerializerOptions s_serializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AzureDevOpsReportingParameters _azdoParameters = azdoParameters;
    private readonly ILogger _logger = logger.OrNull();

    public async Task ReportResultsAsync(IReadOnlyList<TestResult> results, CancellationToken cancellationToken = default)
    {
        var filtered = (results ?? [])
            .Where(static x => x is not null)
            .ToList();

        var serialized = new PackedTestReport(_azdoParameters, filtered);
        string path = Path.Combine(Environment.CurrentDirectory, ReportFileName);
        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _logger.LogInformation("Packing {Count} test reports to '{Path}'", filtered.Count, path);

        await using (FileStream saveFile = File.Create(path))
        {
            await JsonSerializer.SerializeAsync(saveFile, serialized, s_serializerOptions, cancellationToken);
            await saveFile.FlushAsync(cancellationToken);
        }

        _logger.LogInformation("Packed {Length} bytes", new FileInfo(path).Length);
    }

    public static async Task<(AzureDevOpsReportingParameters Parameters, IReadOnlyList<TestResult> Results)?> UnpackResultsAsync(
        string? searchDirectory = null,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        ILogger effectiveLogger = logger.OrNull();
        string? path = null;

        if (!string.IsNullOrWhiteSpace(searchDirectory))
        {
            path = Path.Combine(searchDirectory, ReportFileName);
            if (!File.Exists(path) && Directory.Exists(searchDirectory))
            {
                path = Directory.EnumerateFiles(searchDirectory, ReportFileName, SearchOption.AllDirectories).FirstOrDefault();
            }
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            path = Path.Combine(Environment.CurrentDirectory, ReportFileName);

            if (!File.Exists(path) && !string.IsNullOrWhiteSpace(settings.WorkitemPayloadDir))
            {
                path = Path.Combine(settings.WorkitemPayloadDir, ReportFileName);
            }
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        effectiveLogger.LogInformation("Unpacking {Length} bytes from '{Path}'", new FileInfo(path).Length, path);

        await using FileStream saveFile = File.OpenRead(path);
        PackedTestReport? serialized = await JsonSerializer.DeserializeAsync<PackedTestReport>(saveFile, s_serializerOptions, cancellationToken);
        if (serialized is null)
        {
            effectiveLogger.LogError("Unpacked tests were null or invalid.");
            return null;
        }

        effectiveLogger.LogInformation("Unpacked {Count} test reports", serialized.Results.Count);
        return (serialized.AzdoParameters, serialized.Results);
    }
}
