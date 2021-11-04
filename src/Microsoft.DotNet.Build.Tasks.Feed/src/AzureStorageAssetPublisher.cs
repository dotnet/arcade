// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using Azure.Storage.Blobs;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Maestro.Client.Models;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public abstract class AzureStorageAssetPublisher : IAssetPublisher
    {
        private readonly TaskLoggingHelper _log;

        protected AzureStorageAssetPublisher(TaskLoggingHelper log)
        {
            _log = log;
        }

        public LocationType LocationType => LocationType.Container;

        public abstract BlobClient CreateBlobClient(string blobPath);

        public async Task PublishAssetAsync(string file, string blobPath, PushOptions options, SemaphoreSlim clientThrottle = null)
        {
            using (await SemaphoreLock.LockAsync(clientThrottle))
            {
                blobPath = blobPath.Replace("\\", "/");
                var blobClient = CreateBlobClient(blobPath);
                if (!options.AllowOverwrite && await blobClient.ExistsAsync())
                {
                    if (options.PassIfExistingItemIdentical)
                    {
                        if (!await blobClient.IsFileIdenticalToBlobAsync(file))
                        {
                            _log.LogError($"Asset '{file}' already exists with different contents at '{blobPath}'");
                        }

                        return;
                    }

                    _log.LogError($"Asset '{file}' already exists at '{blobPath}'");
                    return;
                }

                _log.LogMessage($"Uploading '{file}' to '{blobPath}'");
                await blobClient.UploadAsync(file);
            }
        }
    }
}
