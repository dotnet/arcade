// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using System;
using System.Threading;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.CloudTestTasks;
using Microsoft.DotNet.ProductConstructionService.Client.Models;
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
                // Callers build this path from PublishArtifactsInManifestBase.BlobAssetsBasePath, which is a plain
                // task property and may be relative. That task is not opted into multithreading (tracked by
                // https://github.com/dotnet/arcade/issues/17378) and has no TaskEnvironment to resolve against, so
                // fall back to the process working directory - which is what BlobClient.UploadAsync does anyway.
                // Constructing an AbsolutePath from the raw value would instead throw for a relative path.
                AbsolutePath localFile = TaskEnvironment.Fallback.GetAbsolutePath(file);

                blobPath = blobPath.Replace("\\", "/");
                var blobClient = CreateBlobClient(blobPath);
                if (!options.AllowOverwrite && await blobClient.ExistsAsync())
                {
                    if (options.PassIfExistingItemIdentical)
                    {
                        if (!await blobClient.IsFileIdenticalToBlobAsync(localFile))
                        {
                            _log.LogError($"Asset '{file}' already exists with different contents at '{blobClient.Uri}'");
                        }

                        return;
                    }

                    _log.LogError($"Asset '{file}' already exists at '{blobClient.Uri}'");
                    return;
                }

                _log.LogMessage($"Uploading '{file}' to '{blobClient.Uri}'");

                try
                {
                    BlobUploadOptions blobUploadOptions = new()
                    {
                        HttpHeaders = AzureStorageUtils.GetBlobHeadersByExtension(file)
                    };
                    await blobClient.UploadAsync(localFile, blobUploadOptions);
                }
                catch (Exception e)
                {
                    _log.LogError($"Unexpected exception publishing file {file} to {blobClient.Uri}: {e.Message}");
                }
            }
        }
    }
}
