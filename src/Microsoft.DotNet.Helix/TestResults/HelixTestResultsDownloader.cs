// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.Helix.AzureDevOpsTestPublisher;

internal sealed class HelixTestResultsDownloader
{
    private readonly IHelixApi _api;
    private readonly IBlobClientFactory _blobClientFactory;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger _logger;
    private readonly TestReportingMetrics _metrics;

    public HelixTestResultsDownloader(
        IHelixApi api,
        ILogger logger,
        TestReportingMetrics metrics = null,
        IBlobClientFactory blobClientFactory = null,
        IFileSystem fileSystem = null)
    {
        _api = api;
        _logger = logger;
        _metrics = metrics ?? new TestReportingMetrics();
        _blobClientFactory = blobClientFactory ?? new AzureBlobClientFactory();
        _fileSystem = fileSystem ?? new FileSystem();
    }

    public async Task<WorkItemTestResults> DownloadAsync(
        string jobName,
        string workItemName,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        string outputDirectory = _fileSystem.PathCombine(workingDirectory, SanitizeDirName(jobName));
        _fileSystem.CreateDirectory(outputDirectory);
        JobResultsUri resultsUri = await RetryAsync(() => _api.Job.ResultsAsync(jobName, cancellationToken), cancellationToken);
        IReadOnlyList<UploadedFile> availableFiles = await RetryAsync(
            () => _api.WorkItem.ListFilesAsync(workItemName, jobName, false, cancellationToken), cancellationToken);
        List<UploadedFile> resultFiles = [.. availableFiles.Where(f => LocalTestResultsReader.LooksLikeTestResultFile(f.Name))];
        if (resultFiles.Count == 0)
        {
            return new WorkItemTestResults(jobName, workItemName, []);
        }

        string workItemDirectory = _fileSystem.PathCombine(outputDirectory, SanitizeDirName(workItemName));
        _fileSystem.CreateDirectory(workItemDirectory);
        List<string> downloaded = [];
        List<Exception> transientFailures = [];
        foreach (UploadedFile file in resultFiles)
        {
            string relativePath = file.Name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
            string destinationFile = _fileSystem.PathCombine(workItemDirectory, relativePath);
            string directory = _fileSystem.GetDirectoryName(destinationFile);
            if (!string.IsNullOrEmpty(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }
            try
            {
                IBlobClient blobClient = _blobClientFactory.CreateBlobClient(file.Link, resultsUri.ResultsUriRSAS);
                await blobClient.DownloadToAsync(destinationFile, cancellationToken);
                downloaded.Add(destinationFile);
                _metrics.RecordResultBlobDownload(failed: false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (TransientFailureDetector.IsTransient(ex))
            {
                _metrics.RecordResultBlobDownload(failed: true);
                transientFailures.Add(ex);
                _logger.LogWarning(ex,
                    "Transient failure downloading '{FileName}' for '{JobName}/{WorkItemName}'. "
                    + "The remaining files will still be attempted before the work item is retried.",
                    file.Name, jobName, workItemName);
            }
            catch (Exception ex)
            {
                _metrics.RecordResultBlobDownload(failed: true);
                _logger.LogWarning(ex, "Failed to download '{FileName}' for '{JobName}/{WorkItemName}'.", file.Name, jobName, workItemName);
            }
        }
        if (transientFailures.Count > 0)
        {
            throw new IOException(
                $"One or more transient test-result downloads failed for Helix job '{jobName}'.",
                new AggregateException(transientFailures));
        }
        return new WorkItemTestResults(jobName, workItemName, downloaded);
    }

    private static string SanitizeDirName(string value)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalidChar, '-');
        }
        return value;
    }

    private async Task<T> RetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        T result = default;
        Exception last = null;
        int attempt = 0;
        var retry = new ExponentialRetry
        {
            MaxAttempts = 5,
            DelayBase = 2,
            DelayConstant = 0,
            MinRandomFactor = 1,
            MaxRandomFactor = 1,
            RetryDelayCallback = (failedAttempt, delay) =>
                _logger.LogDebug("Transient Helix request failure on attempt {Attempt}. Waiting {Delay}.", failedAttempt, delay),
        };
        bool succeeded = await retry.RunAsync(
            async _ =>
            {
                int currentAttempt = attempt++;
                try
                {
                    result = await action();
                    _metrics.RecordHelixRequest(isRetry: currentAttempt > 0, failed: false);
                    return RetryResult.Success;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    _metrics.RecordHelixRequest(isRetry: currentAttempt > 0, failed: true);
                    if (!TransientFailureDetector.IsTransient(ex))
                    {
                        throw;
                    }
                    last = ex;
                    return RetryResult.Retry();
                }
            }, cancellationToken);
        return succeeded ? result : throw last ?? new InvalidOperationException("Helix request retry failed without capturing an exception.");
    }
}
