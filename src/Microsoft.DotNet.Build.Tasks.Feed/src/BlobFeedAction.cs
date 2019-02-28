// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Build.CloudTestTasks;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json.Linq;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using Sleet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    sealed class BlobFeedAction
    {
        private MSBuild.TaskLoggingHelper Log;
        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;
        private const string feedRegex = @"(?<feedurl>https:\/\/(?<accountname>[^\.-]+)(?<domain>[^\/]*)\/((?<token>[a-zA-Z0-9+\/]*?\/\d{4}-\d{2}-\d{2})\/)?(?<containername>[^\/]+)\/(?<relativepath>.*\/)?)index\.json";
        private string feedUrl;
        private SleetSource source;
        private bool hasToken = false;

        public BlobFeed feed;

        public BlobFeedAction(string expectedFeedUrl, string accountKey, MSBuild.TaskLoggingHelper Log)
        {
            // This blob feed action regex is custom because of the way that NuGet handles query strings (it doesn't)
            // Instead of encoding the query string containing the SAS at the end of the URL we encode it at the beginning.
            // As a result, we can't parse this feed url like a traditional feed url.  When this changes, this code could be simplified and
            // BlobUriParser could be used instead.
            this.Log = Log;
            Match m = Regex.Match(expectedFeedUrl, feedRegex);
            if (m.Success)
            {
                string accountName = m.Groups["accountname"].Value;
                string containerName = m.Groups["containername"].Value;
                string relativePath = m.Groups["relativepath"].Value;
                feed = new BlobFeed(accountName, accountKey, containerName, relativePath, Log);
                feedUrl = m.Groups["feedurl"].Value;
                hasToken = !string.IsNullOrEmpty(m.Groups["token"].Value);

                source = new SleetSource
                {
                    Name = feed.ContainerName,
                    Type = "azure",
                    Path = feedUrl,
                    Container = feed.ContainerName,
                    FeedSubPath = feed.RelativePath,
                    ConnectionString = $"DefaultEndpointsProtocol=https;AccountName={feed.AccountName};AccountKey={feed.AccountKey};EndpointSuffix=core.windows.net"
                };
            }
            else
            {
                throw new Exception("Unable to parse expected feed. Please check ExpectedFeedUrl.");
            }
        }

        public async Task<bool> PushToFeedAsync(
            IEnumerable<string> items,
            PushOptions options)
        {
            if (IsSanityChecked(items))
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task PushToFeed cancelled");
                    CancellationToken.ThrowIfCancellationRequested();
                }

                await PushItemsToFeedAsync(items, options);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task<bool> PushItemsToFeedAsync(
            IEnumerable<string> items,
            PushOptions options)
        {
            Log.LogMessage(MessageImportance.Low, $"START pushing items to feed");

            if (!items.Any())
            {
                Log.LogMessage("No items to push found in the items list.");
                return true;
            }

            try
            {
                bool result = await PushAsync(items, options);
                return result;
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        public async Task PublishToFlatContainerAsync(IEnumerable<ITaskItem> taskItems,
            int maxClients,
            int uploadTimeoutInMinutes,
            PushOptions pushOptions)
        {
            if (taskItems.Any())
            {
                using (var clientThrottle = new SemaphoreSlim(maxClients, maxClients))
                {
                    Log.LogMessage(MessageImportance.High, $"Uploading {taskItems.Count()} items:");
                    await System.Threading.Tasks.Task.WhenAll(taskItems.Select(
                        item =>
                        {
                            Log.LogMessage(MessageImportance.High, $"Async uploading {item.ItemSpec}");
                            return UploadAssetAsync(
                                item,
                                clientThrottle,
                                uploadTimeoutInMinutes,
                                pushOptions);
                        }
                    ));
                }
            }
        }

        public async Task UploadAssetAsync(
            ITaskItem item,
            SemaphoreSlim clientThrottle,
            int uploadTimeout,
            PushOptions options)
        {
            string relativeBlobPath = item.GetMetadata("RelativeBlobPath");

            if (string.IsNullOrEmpty(relativeBlobPath))
            {
                string fileName = Path.GetFileName(item.ItemSpec);
                string recursiveDir = item.GetMetadata("RecursiveDir");
                relativeBlobPath = $"{recursiveDir}{fileName}";
            }

            string contentType = item.GetMetadata("ContentType");

            relativeBlobPath = $"{feed.RelativePath}{relativeBlobPath}".Replace("\\", "/");

            if (relativeBlobPath.Contains("//"))
            {
                Log.LogError(
                    $"Item '{item.ItemSpec}' RelativeBlobPath contains virtual directory " +
                    $"without name (double forward slash): '{relativeBlobPath}'");
                return;
            }

            Log.LogMessage($"Uploading {relativeBlobPath}");

            await clientThrottle.WaitAsync();

            try
            {
                UploadClient uploadClient = new UploadClient(Log);

                if (!options.AllowOverwrite && await feed.CheckIfBlobExistsAsync(relativeBlobPath))
                {
                    if (options.PassIfExistingItemIdentical)
                    {
                        if (!await uploadClient.FileEqualsExistingBlobAsync(
                            feed.AccountName,
                            feed.AccountKey,
                            feed.ContainerName,
                            item.ItemSpec,
                            relativeBlobPath,
                            uploadTimeout))
                        {
                            Log.LogError(
                                $"Item '{item}' already exists with different contents " +
                                $"at '{relativeBlobPath}'");
                        }
                    }
                    else
                    {
                        Log.LogError($"Item '{item}' already exists at '{relativeBlobPath}'");
                    }
                }
                else
                {
                    Log.LogMessage($"Uploading {item} to {relativeBlobPath}.");
                    await uploadClient.UploadBlockBlobAsync(
                        CancellationToken,
                        feed.AccountName,
                        feed.AccountKey,
                        feed.ContainerName,
                        item.ItemSpec,
                        relativeBlobPath,
                        contentType,
                        uploadTimeout);
                }
            }
            catch (Exception exc)
            {
                Log.LogError($"Unable to upload to {relativeBlobPath} due to {exc}.");
                throw;
            }
            finally
            {
                clientThrottle.Release();
            }
        }

        public async Task CreateContainerAsync(IBuildEngine buildEngine, bool publishFlatContainer)
        {
            Log.LogMessage($"Creating container {feed.ContainerName}...");

            CreateAzureContainer createContainer = new CreateAzureContainer
            {
                AccountKey = feed.AccountKey,
                AccountName = feed.AccountName,
                ContainerName = feed.ContainerName,
                FailIfExists = false,
                IsPublic = !hasToken,
                BuildEngine = buildEngine
            };

            await createContainer.ExecuteAsync();

            Log.LogMessage($"Creating container {feed.ContainerName} succeeded!");

            if (!publishFlatContainer)
            {
                try
                {
                    bool result = await InitAsync();

                    if (result)
                    {
                        Log.LogMessage($"Initializing sub-feed {source.FeedSubPath} succeeded!");
                    }
                    else
                    {
                        throw new Exception($"Initializing sub-feed {source.FeedSubPath} failed!");
                    }
                }
                catch (Exception e)
                {
                    Log.LogErrorFromException(e);
                }
            }
        }

        public async Task<ISet<PackageIdentity>> GetPackageIdentitiesAsync()
        {
            using (var fileCache = CreateFileCache())
            {
                var context = new SleetContext
                {
                    LocalSettings = GetSettings(),
                    Log = new SleetLogger(Log, NuGet.Common.LogLevel.Verbose),
                    Source = GetAzureFileSystem(fileCache),
                    Token = CancellationToken
                };
                context.SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(
                    context.Source,
                    context.Log,
                    context.Token);

                var packageIndex = new PackageIndex(context);

                return await packageIndex.GetPackagesAsync();
            }
        }

        private bool IsSanityChecked(IEnumerable<string> items)
        {
            Log.LogMessage(MessageImportance.Low, $"START checking sanitized items for feed");
            foreach (var item in items)
            {
                if (items.Any(s => Path.GetExtension(item) != ".nupkg"))
                {
                    Log.LogError($"{item} is not a nupkg");
                    return false;
                }
            }
            List<string> duplicates = items.GroupBy(x => x)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key).ToList();
            if (duplicates.Count > 0)
            {
                Log.LogError($"Duplicates found: {string.Join(", ", duplicates)}");
                return false;
            }
            Log.LogMessage(MessageImportance.Low, $"DONE checking for sanitized items for feed");
            return true;
        }

        private async Task<bool?> IsPackageIdenticalOnFeedAsync(
            string item,
            PackageIndex packageIndex,
            ISleetFileSystem source,
            FlatContainer flatContainer,
            SleetLogger log)
        {
            using (var package = new PackageArchiveReader(item))
            {
                var id = await package.GetIdentityAsync(CancellationToken);
                if (await packageIndex.Exists(id))
                {
                    using (Stream remoteStream = await source
                        .Get(flatContainer.GetNupkgPath(id))
                        .GetStream(log, CancellationToken))
                    using (var remote = new MemoryStream())
                    {
                        await remoteStream.CopyToAsync(remote);

                        byte[] existingBytes = remote.ToArray();
                        byte[] localBytes = File.ReadAllBytes(item);

                        return existingBytes.SequenceEqual(localBytes);
                    }
                }
                return null;
            }
        }

        private LocalSettings GetSettings()
        {
            SleetSettings sleetSettings = new SleetSettings()
            {
                Sources = new List<SleetSource>
                    {
                       source
                    }
            };

            LocalSettings settings = new LocalSettings
            {
                Json = JObject.FromObject(sleetSettings)
            };

            return settings;
        }

        private AzureFileSystem GetAzureFileSystem(LocalCache fileCache)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(source.ConnectionString);
            AzureFileSystem fileSystem = new AzureFileSystem(fileCache, new Uri(source.Path), new Uri(source.Path), storageAccount, source.Name, source.FeedSubPath);
            return fileSystem;
        }

        private async Task<bool> PushAsync(
            IEnumerable<string> items,
            PushOptions options)
        {
            LocalSettings settings = GetSettings();
            SleetLogger log = new SleetLogger(Log, NuGet.Common.LogLevel.Verbose);
            var packagesToPush = items.ToList();

            // Create a separate LocalCache to use for read only operations on the feed.
            // Files added to the cache before the lock could be modified by the process
            // currently holding the lock. Sleet assumes that files in the cache 
            // are valid and identical to the ones on the feed since operations are 
            // normally performed inside the lock.
            using (var preLockCache = CreateFileCache())
            {
                AzureFileSystem preLockFileSystem = GetAzureFileSystem(preLockCache);

                if (!options.AllowOverwrite && options.PassIfExistingItemIdentical)
                {
                    var context = new SleetContext
                    {
                        LocalSettings = settings,
                        Log = log,
                        Source = preLockFileSystem,
                        Token = CancellationToken
                    };
                    context.SourceSettings = await FeedSettingsUtility.GetSettingsOrDefault(
                        context.Source,
                        context.Log,
                        context.Token);

                    var flatContainer = new FlatContainer(context);

                    var packageIndex = new PackageIndex(context);

                    // Check packages sequentially: Task.WhenAll caused IO exceptions in Sleet.
                    for (int i = packagesToPush.Count - 1; i >= 0; i--)
                    {
                        string item = packagesToPush[i];

                        bool? identical = await IsPackageIdenticalOnFeedAsync(
                            item,
                            packageIndex,
                            context.Source,
                            flatContainer,
                            log);

                        if (identical == null)
                        {
                            continue;
                        }

                        packagesToPush.RemoveAt(i);

                        if (identical == true)
                        {
                            Log.LogMessage(
                                MessageImportance.Normal,
                                "Package exists on the feed, and is verified to be identical. " +
                                $"Skipping upload: '{item}'");
                        }
                        else
                        {
                            Log.LogError(
                                "Package exists on the feed, but contents are different. " +
                                $"Upload failed: '{item}'");
                        }
                    }

                    if (!packagesToPush.Any())
                    {
                        Log.LogMessage("After skipping idempotent uploads, no items need pushing.");
                        return true;
                    }
                }
            }

            // Create a new cache to be used once a lock is obtained.
            using (var fileCache = CreateFileCache())
            {
                var lockedFileSystem = GetAzureFileSystem(fileCache);

                return await PushCommand.RunAsync(
                    settings,
                    lockedFileSystem,
                    packagesToPush,
                    options.AllowOverwrite,
                    skipExisting: false,
                    log: log);
            }
        }

        private async Task<bool> InitAsync()
        {
            using (var fileCache = CreateFileCache())
            {
                LocalSettings settings = GetSettings();
                AzureFileSystem fileSystem = GetAzureFileSystem(fileCache);
                bool result = await InitCommand.RunAsync(settings, fileSystem, enableCatalog: false, enableSymbols: false, log: new SleetLogger(Log, NuGet.Common.LogLevel.Verbose), token: CancellationToken);
                return result;
            }
        }

        private static LocalCache CreateFileCache()
        {
            // By default a folder is created inside %temp% to store the cache, to 
            // change this location pass a folder path to the LocalCache constructor.
            // Passing PerfTracker in so a summary is logged at the end of publishing.
            return new LocalCache(new PerfTracker());
        }
    }
}
