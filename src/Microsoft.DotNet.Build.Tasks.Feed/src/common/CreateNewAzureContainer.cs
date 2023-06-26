// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.DotNet.Build.CloudTestTasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{ 
    public class CreateNewAzureContainer : CreateAzureContainer
    {
        public override async Task<AzureStorageUtils> GetBlobStorageUtilsAsync(string accountName, string accountKey, string containerName)
        {
            int version = 0;
            string versionedContainerName = containerName;

            AzureStorageUtils blobUtils;
            bool needsUniqueName;
            do
            {
                blobUtils = new AzureStorageUtils(accountName, accountKey, versionedContainerName);
                if (await blobUtils.CheckIfContainerExistsAsync())
                {
                    versionedContainerName = $"{containerName}-{++version}";
                    needsUniqueName = true;
                }
                else
                {
                    needsUniqueName = false;
                    ContainerName = versionedContainerName;
                }
            }
            while (needsUniqueName);

            return blobUtils;
        }
    }
}
