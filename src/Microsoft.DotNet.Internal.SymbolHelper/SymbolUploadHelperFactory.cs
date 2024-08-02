// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable enable
using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Azure.Core;
using Microsoft.SymbolStore;
using Polly;
using Polly.Retry;

namespace Microsoft.DotNet.Internal.SymbolHelper;

public class SymbolUploadHelperFactory
{
    private static readonly HttpClient s_symbolDownloadClient = new();

    /// <summary>
    /// Gets a <see cref="SymbolUploadHelper"/> instance, downloading the client for the appropriate tenant.
    /// </summary>
    /// <param name="logger">An <see cref="ITracer"/> instance to log to and pass to the client.</param>
    /// <param name="options">The options for the symbol upload client.</param>
    /// <param name="installDirectory">Optional. The directory to install the symbol tool. This folder will get cleaned before download. If not supplied, a random temporary folder is used.</param>
    /// <param name="retryCount">Optional. The number of times to retry the download for transient errors. Defaults to 3.</param>
    /// <param name="token">Optional. The cancellation token to use during symbol download.</param>
    /// <returns>A <see cref="SymbolUploadHelper"/> instance for the tenant's symbol server version.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="logger"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">If the host is not supported for symbol publishing.</exception>
    /// <exception cref="InvalidOperationException">If the download response does not contain the expected URI.</exception>
    /// <exception cref="HttpRequestException">If the symbol client download fails after retries.</exception>
    /// <exception cref="FileNotFoundException">If the symbol tool is not found after download.</exception>
    public static async Task<SymbolUploadHelper> GetSymbolHelperWithDownloadAsync(ITracer logger, SymbolPublisherOptions options, string? installDirectory = null, string? workingDir = null, int retryCount = 3, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfHostUnsupported();

        installDirectory ??= Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        if (Directory.Exists(installDirectory))
        {
            Directory.Delete(installDirectory, recursive: true);
        }

        _ = Directory.CreateDirectory(installDirectory);

        string localToolPath = await DownloadSymbolsToolAsync(logger, options.Tenant, options.Credential, installDirectory, retryCount, token);

        return GetSymbolHelperFromLocalTool(logger, options, localToolPath, workingDir);
    }

    /// <summary>
    /// Gets a <see cref="SymbolUploadHelper"/> instance from a local available client tool.
    /// </summary>
    /// <param name="logger">An <see cref="ITracer"/> instance to log to and pass to the client.</param>
    /// <param name="symbolToolDirectory">The directory containing the symbol tool.</param>
    /// <param name="options">The options for the symbol upload client.</param>
    /// <exception cref="ArgumentNullException">If <paramref name="logger"/> or <paramref name="options"/> is null.</exception>
    /// <exception cref="InvalidOperationException">If the host is not supported for symbol publishing.</exception>
    /// <exception cref="FileNotFoundException">If the symbol tool is not found after download.</exception>
    public static SymbolUploadHelper GetSymbolHelperFromLocalTool(ITracer logger, SymbolPublisherOptions options, string symbolToolDirectory, string? workingDir = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ThrowIfHostUnsupported();

        string expectedSymbolPath = GetSymbolToolPathFromInstallDir(symbolToolDirectory);

        if (!options.IsDryRun && !File.Exists(expectedSymbolPath))
        {
            logger.Error($"Symbol tool not found at {expectedSymbolPath}");
            throw new FileNotFoundException("Symbol tool not found", expectedSymbolPath);
        }

        return new SymbolUploadHelper(logger, expectedSymbolPath, options, workingDir);
    }

    /// <exception cref="ArgumentNullException">If <paramref name="logger"/>, <paramref name="credential"/>, or <paramref name="installDirectory"/> is null.</exception>
    /// <exception cref="ArgumentException">If <paramref name="tenant"/> is null or empty.</exception>
    /// <exception cref="InvalidOperationException">If the host is not supported for symbol publishing.</exception>
    /// <exception cref="InvalidOperationException">If the download response does not contain the expected URI.</exception>
    /// <exception cref="HttpRequestException">If the symbol client download fails after retries.</exception>
    /// <exception cref="FileNotFoundException">If the symbol tool is not found after download.</exception>
    private static async Task<string> DownloadSymbolsToolAsync(
        ITracer logger,
        string tenant, TokenCredential credential,
        string installDirectory, int retryCount = 3, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(installDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenant);
        ThrowIfHostUnsupported();

        ResiliencePipeline<string> pipeline = new ResiliencePipelineBuilder<string>()
            .AddRetry(new RetryStrategyOptions<string>
            {
                ShouldHandle = static args =>
                {
                    if (args.Outcome.Exception is null) { return ValueTask.FromResult(false); }
                    if (args.Outcome.Exception is HttpRequestException httpException)
                    {
                        bool isRetryable = (httpException.StatusCode == HttpStatusCode.Unauthorized && args.AttemptNumber == 0) // In case the token was grabbed from cache and died shortly. Retry only once in this case.
                            || httpException.StatusCode == HttpStatusCode.RequestTimeout
                            || httpException.StatusCode == HttpStatusCode.TooManyRequests
                            || httpException.StatusCode == HttpStatusCode.BadGateway
                            || httpException.StatusCode == HttpStatusCode.ServiceUnavailable
                            || httpException.StatusCode == HttpStatusCode.GatewayTimeout;
                        return ValueTask.FromResult(isRetryable);
                    }
                    return ValueTask.FromResult(false);
                },
                Delay = TimeSpan.FromSeconds(30),
                MaxRetryAttempts = retryCount,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                MaxDelay = TimeSpan.FromMinutes(5),
                OnRetry = args =>
                {
                    if (args.Outcome.Exception is HttpRequestException httpException)
                    {
                        logger.Information("Try {0} failed with '{1}', delaying {2}", args.AttemptNumber + 1, httpException.Message, args.RetryDelay);
                    }
                    else
                    {
                        logger.Information("Try {0} failed, delaying {1}", args.AttemptNumber, args.RetryDelay);
                    }
                    return default;
                }
            })
            .Build();

        string toolUrlWithSas = await pipeline.ExecuteAsync(async token => await GetToolUrl(logger, tenant, credential, token), token);
        string toolZipPath = await pipeline.ExecuteAsync(async token => await DownloadTool(logger, toolUrlWithSas, installDirectory, token), token);

        using ZipArchive archive = ZipFile.OpenRead(toolZipPath);
        archive.ExtractToDirectory(installDirectory);

        return installDirectory;

        static async Task<string> GetToolUrl(ITracer logger, string tenant, TokenCredential credential, CancellationToken token)
        {
            string downloadUri = $"https://vsblob.dev.azure.com/{tenant}/_apis/clienttools/symbol/release?osName=windows&arch=x86_64";

            logger.Information($"Fetching symbol tool location from {downloadUri}");

            const string AzureDevOpsResource = "499b84ac-1321-427f-aa17-267ca6975798";
            AccessToken azdoToken = await credential.GetTokenAsync(new([AzureDevOpsResource]), token).ConfigureAwait(false);

            using HttpRequestMessage getUriRequest = new(HttpMethod.Get, downloadUri)
            {
                Headers =
                {
                    Authorization = new ("Bearer", azdoToken.Token),
                    Accept = { new ("application/json") },
                }
            };

            // Suppress the redirect to the login page if the token is invalid
            getUriRequest.Headers.Add("X-TFS-FedAuthRedirect", "Suppress");

            using HttpResponseMessage response = await s_symbolDownloadClient.SendAsync(getUriRequest, token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            Stream jsonResponse = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            JsonDocument jsonDocument = await JsonDocument.ParseAsync(jsonResponse, cancellationToken: token);

            if (!jsonDocument.RootElement.TryGetProperty("uri", out JsonElement uriElement))
            {
                throw new InvalidOperationException($"Failed to get download URL for symbol tool in tenant {tenant}");
            }

            string toolUrlWithSas = uriElement.GetString() ?? throw new InvalidOperationException("URI field of symbol download response not in expected format.");
            return toolUrlWithSas;
        }

        static async Task<string> DownloadTool(ITracer logger, string toolUrlWithSas, string installDirectory, CancellationToken token)
        {
            Uri toolUri = new(toolUrlWithSas);
            string secretlessUri = toolUri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped);
            logger.Information($"Downloading symbol tool from {secretlessUri} to {installDirectory}");

            HttpRequestMessage getToolRequest = new(HttpMethod.Get, toolUrlWithSas)
            {
                Headers = { Accept = { new("application/zip") } }
            };

            using HttpResponseMessage symbolDownloadResponse = await s_symbolDownloadClient.SendAsync(getToolRequest, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            symbolDownloadResponse.EnsureSuccessStatusCode();

            string zipFilePath = Path.Combine(installDirectory, "symbol.zip");
            using (FileStream fileStream = new(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (Stream zipStream = await symbolDownloadResponse.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
            {
                await zipStream.CopyToAsync(fileStream, token).ConfigureAwait(false);
            }

            logger.Information($"Successfully downloaded tool from {zipFilePath}");
            return zipFilePath;
        }
    }

    private static string GetSymbolToolPathFromInstallDir(string installDirectory) => Path.Combine(installDirectory, "symbol.exe");

    // This method is used to ensure that the host is supported for symbol publishing.
    // We rely on DIA for symbol conversion (windows only) and on x64 for the upload client.
    private static void ThrowIfHostUnsupported()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            throw new InvalidOperationException("Symbol publishing currently relies on Windows x64 hosting");
        }
    }
}
