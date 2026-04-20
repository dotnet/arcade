// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestReporter;

public sealed class PackingTestReporter : ITestReporter
{
    private const string ReportFileName = "__test_report.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly AzureDevOpsReportingParameters _azdoParameters;
    private readonly ILogger _logger;

    public PackingTestReporter(AzureDevOpsReportingParameters azdoParameters, ILogger? logger = null)
    {
        _azdoParameters = azdoParameters;
        _logger = logger.OrNull();
    }

    public async Task ReportResultsAsync(IReadOnlyList<TestResult> results, CancellationToken cancellationToken = default)
    {
        var filtered = (results ?? Array.Empty<TestResult>())
            .Where(static x => x is not null)
            .ToList();

        var serialized = new PackedTestReport(_azdoParameters, filtered);
        var path = GetFileName();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _logger.LogInformation("Packing {Count} test reports to '{Path}'", filtered.Count, path);

        await using (var saveFile = File.Create(path))
        {
            await JsonSerializer.SerializeAsync(saveFile, serialized, SerializerOptions, cancellationToken);
            await saveFile.FlushAsync(cancellationToken);
        }

        _logger.LogInformation("Packed {Length} bytes", new FileInfo(path).Length);
    }

    public static string GetFileName(HelixEnvironmentSettings? settings = null)
    {
        settings ??= HelixEnvironmentSettings.FromEnvironment();
        var root = settings.WorkitemWorkingDir;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Environment.CurrentDirectory;
        }

        return Path.Combine(root, ReportFileName);
    }

    public static async Task<(AzureDevOpsReportingParameters Parameters, IReadOnlyList<TestResult> Results)?> UnpackResultsAsync(
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveLogger = logger.OrNull();
        var settings = HelixEnvironmentSettings.FromEnvironment();
        var path = GetFileName(settings);

        if (!File.Exists(path) && !string.IsNullOrWhiteSpace(settings.WorkitemPayloadDir))
        {
            path = Path.Combine(settings.WorkitemPayloadDir, ReportFileName);
        }

        if (!File.Exists(path))
        {
            return null;
        }

        effectiveLogger.LogInformation("Unpacking {Length} bytes from '{Path}'", new FileInfo(path).Length, path);

        await using var saveFile = File.OpenRead(path);
        var serialized = await JsonSerializer.DeserializeAsync<PackedTestReport>(saveFile, SerializerOptions, cancellationToken);
        if (serialized is null)
        {
            effectiveLogger.LogError("Unpacked tests were null or invalid.");
            return null;
        }

        effectiveLogger.LogInformation("Unpacked {Count} test reports", serialized.Results.Count);
        return (serialized.AzdoParameters, serialized.Results);
    }
}
