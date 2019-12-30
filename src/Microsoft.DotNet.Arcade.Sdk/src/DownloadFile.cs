// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Tasks = System.Threading.Tasks;

namespace Microsoft.DotNet.Arcade.Sdk
{
    public class DownloadFile : Task, ICancelableTask
    {
        /// <summary>
        /// List of URls to attempt download from. Accepted metadata are:
        ///     - Token: Base64 encoded token to be appended to base URL for accessing private locations.
        /// </summary>
        public ITaskItem[] Uris { get; set; }

        public string Uri { get; set; }

        [Required]
        public string DestinationPath { get; set; }

        public bool Overwrite { get; set; }

        /// <summary>
        /// Delay between any necessary retries.
        /// </summary>
        public int RetryDelayMilliseconds { get; set; } = 1000;

        public int Retries { get; set; } = 3;

        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

        public void Cancel() => _cancellationSource.Cancel();

        public override bool Execute()
        {
            if (Retries < 0)
            {
                Log.LogError($"Invalid task parameter value: Retries={Retries}");
                return false;
            }

            if (RetryDelayMilliseconds < 0)
            {
                Log.LogError($"Invalid task parameter value: RetryDelayMilliseconds={RetryDelayMilliseconds}");
                return false;
            }

            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(Uri) && (Uris == null || Uris.Count() == 0)) {
                Log.LogError($"Invalid task parameter value: Uri and Uris are empty.");
                return false;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(DestinationPath));

            if (!string.IsNullOrWhiteSpace(Uri)) {
                return DownloadBruto(Uri).Result;
            }

            if (Uris != null) {
                foreach (var uriConfig in Uris)
                {
                    var uri = uriConfig.ItemSpec;
                    var encodedToken = uriConfig.GetMetadata("token");

                    if (!string.IsNullOrWhiteSpace(encodedToken))
                    {
                        var encodedTokenBytes = System.Convert.FromBase64String(encodedToken);
                        var decodedToken = System.Text.Encoding.UTF8.GetString(encodedTokenBytes);
                        uri = $"{uri}{decodedToken}";
                    }

                    if (DownloadBruto(uri).Result) {
                        return true;
                    }
                }
            }

            return false;
        }

        private async Tasks.Task<bool> DownloadBruto(string uri) {
            const string FileUriProtocol = "file://";

            if (uri.StartsWith(FileUriProtocol, StringComparison.Ordinal))
            {
                var filePath = uri.Substring(FileUriProtocol.Length);
                Log.LogMessage($"Copying '{filePath}' to '{DestinationPath}'");
                File.Copy(filePath, DestinationPath, overwrite: true);
                return true;
            }

            Log.LogMessage($"Downloading '{uri}' to '{DestinationPath}'");

            using (var httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
            {
                try
                {
                    return await DownloadAsync(httpClient, uri);
                }
                catch (AggregateException e)
                {
                    if (e.InnerException is OperationCanceledException)
                    {
                        Log.LogError($"Download of '{uri}' to '{DestinationPath}' has been cancelled.");
                        return false;
                    }

                    throw e.InnerException;
                }
            }
        }

        private async Tasks.Task<bool> DownloadAsync(HttpClient client, string uri)
        {            
            int attempt = 0;

            while (true)
            {
                try
                {
                    var stream = await client.GetStreamAsync(uri).ConfigureAwait(false);

                    using (var outStream = File.Create(DestinationPath))
                    {
                        await stream.CopyToAsync(outStream, bufferSize: 81920, cancellationToken: _cancellationSource.Token).ConfigureAwait(false);
                    }

                    return true;
                }
                catch (Exception e) when (e is HttpRequestException || e is IOException && !(e is DirectoryNotFoundException || e is PathTooLongException))
                {
                    attempt++;

                    if (attempt > Retries)
                    {
                        Log.LogError($"Failed to download '{uri}' to '{DestinationPath}'");
                        return false;
                    }

                    Log.LogWarning($"Retrying download of '{uri}' to '{DestinationPath}' due to failure: '{e.Message}' ({attempt}/{Retries})");

                    await Tasks.Task.Delay(RetryDelayMilliseconds).ConfigureAwait(false);
                    continue;
                }
            }
        }
    }
}

