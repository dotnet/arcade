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
        [Required]
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

            Directory.CreateDirectory(Path.GetDirectoryName(DestinationPath));

            const string FileUriProtocol = "file://";

            if (Uri.StartsWith(FileUriProtocol, StringComparison.Ordinal))
            {
                var filePath = Uri.Substring(FileUriProtocol.Length);
                Log.LogMessage($"Copying '{filePath}' to '{DestinationPath}'");
                File.Copy(filePath, DestinationPath, overwrite: true);
                return true;
            }

            Log.LogMessage($"Downloading '{Uri}' to '{DestinationPath}'");

            using (var httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }))
            {
                try
                {
                    return DownloadAsync(httpClient).Result;
                }
                catch (AggregateException e)
                {
                    if (e.InnerException is OperationCanceledException)
                    {
                        Log.LogError($"Download of '{Uri}' to '{DestinationPath}' has been cancelled.");
                        return false;
                    }

                    throw e.InnerException;
                }
            }
        }

        private async Tasks.Task<bool> DownloadAsync(HttpClient client)
        {            
            int attempt = 0;

            while (true)
            {
                try
                {
                    var stream = await client.GetStreamAsync(Uri).ConfigureAwait(false);

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
                        Log.LogError($"Failed to download '{Uri}' to '{DestinationPath}'");
                        return false;
                    }

                    Log.LogWarning($"Retrying download of '{Uri}' to '{DestinationPath}' due to failure: '{e.Message}' ({attempt}/{Retries})");

                    await Tasks.Task.Delay(RetryDelayMilliseconds).ConfigureAwait(false);
                    continue;
                }
            }
        }
    }
}

