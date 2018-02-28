// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.IO
{
    /// <summary>
    /// Downloads a file.
    /// </summary>
    public class DownloadFile : Microsoft.Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        /// <summary>
        /// The file to download. Can be prefixed with <c>file://</c> for local file paths.
        /// </summary>
        [Required]
        public string Uri { get; set; }

        /// <summary>
        /// Destination for the downloaded file.
        /// </summary>
        [Required]
        public string OutputPath { get; set; }

        /// <summary>
        /// Overwrite <see cref="OutputPath"/> if it already exists. Defaults to false.
        /// </summary>
        public bool Overwrite { get; set; }

        /// <summary>
        /// The maximum amount of time to allow for downloading the file. Defaults to 15 minutes.
        /// </summary>
        public int TimeoutSeconds { get; set; } = 60 * 15;

        public void Cancel() => _cts.Cancel();

        public override bool Execute() => ExecuteAsync().Result;

        public async Task<bool> ExecuteAsync()
        {
            if (File.Exists(OutputPath) && !Overwrite)
            {
                Log.LogError($"{OutputPath} already exists. Set Overwrite=true to replace it.");
                return false;
            }

            _cts.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));

            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));

            const string FileUriProtocol = "file://";

            if (Uri.StartsWith(FileUriProtocol, StringComparison.OrdinalIgnoreCase))
            {
                var filePath = Uri.Substring(FileUriProtocol.Length);
                Log.LogMessage($"Copying '{filePath}' to '{OutputPath}'");
                File.Copy(filePath, OutputPath, Overwrite);
            }
            else
            {
                Log.LogMessage($"Downloading '{Uri}' to '{OutputPath}'");

                using (var httpClient = new HttpClient
                {
                    // Set operation timeout to 2 minutes (doesn't represent overall timeout)
                    Timeout = TimeSpan.FromMinutes(2),
                })
                {
                    try
                    {
                        // Only fetch the headers first. This operation will timeout in 2 minutes if headers are not returned.
                        var response = await httpClient.GetAsync(Uri, HttpCompletionOption.ResponseHeadersRead, _cts.Token);

                        response.EnsureSuccessStatusCode();
                        _cts.Token.ThrowIfCancellationRequested();

                        // Get a stream that represents the body. This operation will timeout in 2 minutes if body response doesn't begin.
                        var responseStream = await response.Content.ReadAsStreamAsync();

                        _cts.Token.ThrowIfCancellationRequested();

                        using (var outStream = File.Create(OutputPath))
                        {
                            await responseStream.CopyToAsync(outStream, 4096, _cts.Token);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.LogError($"Downloading '{Uri}' failed.");
                        Log.LogErrorFromException(ex, showStackTrace: true);

                        if (File.Exists(OutputPath))
                        {
                            // cleanup any partially downloaded results.
                            File.Delete(OutputPath);
                        }

                        return false;
                    }
                }
            }

            return !Log.HasLoggedErrors;
        }
    }
}
