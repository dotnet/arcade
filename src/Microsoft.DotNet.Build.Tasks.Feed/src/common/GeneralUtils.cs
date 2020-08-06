// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.CloudTestTasks;
using Microsoft.DotNet.VersionTools.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class GeneralUtils
    {
        public const string SymbolPackageSuffix = ".symbols.nupkg";
        public const string PackageSuffix = ".nupkg";
        public const string PackagesCategory = "PACKAGE";
        public const int MaxRetries = 1;

        /// <summary>
        ///  Enum describing the states of a given package on a feed
        /// </summary>
        public enum PackageFeedStatus
        {
            DoesNotExist,
            ExistsAndIdenticalToLocal,
            ExistsAndDifferent,
            Unknown
        }

        /// <summary>
        ///     Compare a local stream and a remote stream for quality
        /// </summary>
        /// <param name="localFileStream">Local stream</param>
        /// <param name="remoteStream">Remote stream</param>
        /// <param name="bufferSize">Buffer to keep around</param>
        /// <returns>True if the streams are equal, false otherwise.</returns>
        public static async Task<bool> CompareStreamsAsync(Stream localFileStream, Stream remoteStream, int bufferSize)
        {
            byte[] localBuffer = new byte[bufferSize];
            byte[] remoteBuffer = new byte[bufferSize];
            int localBufferWriteOffset = 0;
            int remoteBufferWriteOffset = 0;
            int localBufferReadOffset = 0;
            int remoteBufferReadOffset = 0;

            do
            {
                int localBytesToRead = bufferSize - localBufferWriteOffset;
                int remoteBytesToRead = bufferSize - remoteBufferWriteOffset;

                int bytesRemoteFile = 0;
                int bytesLocalFile = 0;
                if (remoteBytesToRead > 0)
                {
                    bytesRemoteFile = await remoteStream.ReadAsync(remoteBuffer, remoteBufferWriteOffset, remoteBytesToRead);
                }

                if (localBytesToRead > 0)
                {
                    bytesLocalFile = await localFileStream.ReadAsync(localBuffer, localBufferWriteOffset, localBytesToRead);
                }

                int bytesLocalAvailable = bytesLocalFile + (localBufferWriteOffset - localBufferReadOffset);
                int bytesRemoteAvailable = bytesRemoteFile + (remoteBufferWriteOffset - remoteBufferReadOffset);
                int minBytesAvailable = Math.Min(bytesLocalAvailable, bytesRemoteAvailable);

                if (minBytesAvailable == 0)
                {
                    // If there is nothing left to compare (EOS), then good to go.
                    // Otherwise, one stream reached EOS before the other.
                    return bytesLocalFile == bytesRemoteFile;
                }

                // Compare the minimum number of bytes between the two streams, starting at the offset,
                // then advance the offsets for the next pass
                for (int i = 0; i < minBytesAvailable; i++)
                {
                    if (remoteBuffer[remoteBufferReadOffset + i] != localBuffer[localBufferReadOffset + i])
                    {
                        return false;
                    }
                }

                // Advance the offsets. The read offset gets advanced by the amount that we actually compared,
                // While the write offset gets advanced by the amount each of the streams returned.
                localBufferReadOffset += minBytesAvailable;
                remoteBufferReadOffset += minBytesAvailable;

                localBufferWriteOffset += bytesLocalFile;
                remoteBufferWriteOffset += bytesRemoteFile;

                if (localBufferReadOffset == bufferSize)
                {
                    localBufferReadOffset = 0;
                    localBufferWriteOffset = 0;
                }

                if (remoteBufferReadOffset == bufferSize)
                {
                    remoteBufferReadOffset = 0;
                    remoteBufferWriteOffset = 0;
                }
            }
            while (true);
        }

        /// <summary>
        ///     Determine whether a local package is the same as a package on an AzDO feed.
        /// </summary>
        /// <param name="localPackageFullPath"></param>
        /// <param name="packageContentUrl"></param>
        /// <param name="client"></param>
        /// <returns></returns>
        /// <remarks>
        ///     Open a stream to the local file and an http request to the package. There are a couple possibilities:
        ///     - The returned headers includes a content MD5 header, in which case we can
        ///       hash the local file and just compare those.
        ///     - No content MD5 hash, and the streams must be compared in blocks. This is a bit trickier to do efficiently,
        ///       since we do not necessarily want to read all bytes if we can help it. Thus, we should compare in blocks.  However,
        ///       the streams make no gaurantee that they will return a full block each time when read operations are performed, so we
        ///       must be sure to only compare the minimum number of bytes returned.
        /// </remarks>
        public static async Task<PackageFeedStatus> CompareLocalPackageToFeedPackage(string localPackageFullPath, string packageContentUrl, HttpClient client, TaskLoggingHelper log)
        {
            log.LogMessage($"Getting package content from {packageContentUrl} and comparing to {localPackageFullPath}");

            PackageFeedStatus result = PackageFeedStatus.Unknown;

            ExponentialRetry RetryHandler = new ExponentialRetry
            {
                MaxAttempts = MaxRetries
            };

            bool success = await RetryHandler.RunAsync(async attempt =>
            {
                try
                {
                    using (Stream localFileStream = File.OpenRead(localPackageFullPath))
                    using (HttpResponseMessage response = await client.GetAsync(packageContentUrl))
                    {
                        response.EnsureSuccessStatusCode();

                        // Check the headers for content length and md5 
                        bool md5HeaderAvailable = response.Headers.TryGetValues("Content-MD5", out var md5);
                        bool lengthHeaderAvailable = response.Headers.TryGetValues("Content-Length", out var contentLength);

                        if (lengthHeaderAvailable && long.Parse(contentLength.Single()) != localFileStream.Length)
                        {
                            log.LogMessage(MessageImportance.Low, $"Package '{localPackageFullPath}' has different length than remote package '{packageContentUrl}'.");
                            result = PackageFeedStatus.ExistsAndDifferent;
                            return true;
                        }

                        if (md5HeaderAvailable)
                        {
                            var localMD5 = AzureStorageUtils.CalculateMD5(localPackageFullPath);
                            if (!localMD5.Equals(md5.Single(), StringComparison.OrdinalIgnoreCase))
                            {
                                log.LogMessage(MessageImportance.Low, $"Package '{localPackageFullPath}' has different MD5 hash than remote package '{packageContentUrl}'.");
                            }

                            result = PackageFeedStatus.ExistsAndDifferent;
                            return true;
                        }

                        const int BufferSize = 64 * 1024;

                        // Otherwise, compare the streams
                        var remoteStream = await response.Content.ReadAsStreamAsync();
                        var streamsMatch = await GeneralUtils.CompareStreamsAsync(localFileStream, remoteStream, BufferSize);
                        result = streamsMatch ? PackageFeedStatus.ExistsAndIdenticalToLocal : PackageFeedStatus.ExistsAndDifferent;
                        return true;
                    }
                }
                // String based comparison because the status code isn't exposed in HttpRequestException
                // see here: https://github.com/dotnet/runtime/issues/23648
                catch (HttpRequestException e)
                {
                    if (e.Message.Contains("404 (Not Found)"))
                    {
                        result = PackageFeedStatus.DoesNotExist;
                        return true;
                    }

                    // Retry this. Could be an http client timeout, 500, etc.
                    return false;
                }
            });

            return result;
        }

        /// <summary>
        ///     Determine whether the feed is public or private.
        /// </summary>
        /// <param name="feedUrl">Feed url to test</param>
        /// <returns>True if the feed is public, false if it is private, and null if it was not possible to determine.</returns>
        /// <remarks>
        /// Do an unauthenticated GET on the feed URL. If it succeeds, the feed is not public.
        /// If it fails with a 4* error, assume it is internal.
        /// </remarks>
        public static async Task<bool?> IsFeedPublicAsync(string feedUrl, HttpClient httpClient, TaskLoggingHelper log)
        {
            bool? isPublic = null;

            ExponentialRetry RetryHandler = new ExponentialRetry
            {
                MaxAttempts = MaxRetries
            };

            bool success = await RetryHandler.RunAsync(async attempt =>
            {
                try
                {
                    HttpResponseMessage response = await httpClient.GetAsync(feedUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        isPublic = true;
                        return true;
                    }
                    else if (response.StatusCode >= (System.Net.HttpStatusCode)400 &&
                              response.StatusCode < (System.Net.HttpStatusCode)500)
                    {
                        isPublic = false;
                        return true;
                    }
                    else
                    {
                        // Don't know for certain, retry
                        return false;
                    }
                }
                catch (Exception e)
                {
                    log.LogMessage(MessageImportance.Low, $"Unexpected exception {e.Message} when attempting to determine whether feed is internal.");
                    return false;
                }
            });

            if (!success)
            {
                // We couldn't determine anything.  We'd be unlikely to be able to push to this feed either,
                // since it's 5xx'ing.
                log.LogError($"Unable to determine whether '{feedUrl}' is public or internal.");
            }

            return isPublic;
        }

        /// <summary>
        ///     Infers the category based on the extension of the particular asset
        ///     
        ///     If no category can be inferred, then "OTHER" is used.
        /// </summary>
        /// <param name="assetId">ID of asset</param>
        /// <returns>Asset cateogry</returns>
        public static string InferCategory(string assetId, TaskLoggingHelper log)
        {
            var extension = Path.GetExtension(assetId).ToUpper();

            var whichCategory = new Dictionary<string, string>()
            {
                { ".NUPKG", PackagesCategory },
                { ".PKG", "OSX" },
                { ".DEB", "DEB" },
                { ".RPM", "RPM" },
                { ".NPM", "NODE" },
                { ".ZIP", "BINARYLAYOUT" },
                { ".MSI", "INSTALLER" },
                { ".SHA", "CHECKSUM" },
                { ".SHA512", "CHECKSUM" },
                { ".POM", "MAVEN" },
                { ".VSIX", "VSIX" },
                { ".CAB", "BINARYLAYOUT" },
                { ".TAR", "BINARYLAYOUT" },
                { ".GZ", "BINARYLAYOUT" },
                { ".TGZ", "BINARYLAYOUT" },
                { ".EXE", "INSTALLER" },
                { ".SVG", "BADGE"},
                { ".WIXLIB", "INSTALLER" },
                { ".WIXPDB", "INSTALLER" },
                { ".JAR", "INSTALLER" },
                { ".VERSION", "INSTALLER"},
                { ".SWR", "INSTALLER" }
            };

            if (whichCategory.TryGetValue(extension, out var category))
            {
                // Special handling for symbols.nupkg. There are typically plenty of
                // periods in package names. We get the extension to identify nupkg
                // assets. But symbol packages have the extension '.symbols.nupkg'.
                // We want to divide these into a separate category because for stabilized builds,
                // they should go to an isolated location. In a non-stabilized build, they can go straight
                // to blob feeds because the blob feed push tasks will automatically push them to the assets.
                if (assetId.EndsWith(SymbolPackageSuffix, StringComparison.OrdinalIgnoreCase))
                {
                    return "SYMBOLS";
                }
                return category;
            }
            else
            {
                log.LogMessage(MessageImportance.High, $"Defaulting to category 'OTHER' for asset {assetId}");
                return "OTHER";
            }
        }

        /// <summary>
        ///     Start a process as an async Task.
        /// </summary>
        /// <param name="path">Path to process</param>
        /// <param name="arguments">Process arguments</param>
        /// <returns>Process return code</returns>
        public static Task<int> StartProcessAsync(string path, string arguments)
        {
            ProcessStartInfo info = new ProcessStartInfo(path, arguments)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
            };

            Process process = new Process
            {
                StartInfo = info,
                EnableRaisingEvents = true
            };

            var completionSource = new TaskCompletionSource<int>();

            process.Exited += (obj, args) =>
            {
                completionSource.SetResult(((Process)obj).ExitCode);
                process.Dispose();
            };

            process.Start();

            return completionSource.Task;
        }
    }
}
