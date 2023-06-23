// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Microsoft.DotNet.Build.CloudTestTasks;

namespace Microsoft.DotNet.Build.Tasks.Feed
{ 
    public class CreateUniqueAzureContainer : CreateAzureContainer
    {
        public override async Task<AzureStorageUtils> CreateBlobStorageUtilsAsync()
        {
            int version = 0;
            string versionedContainerName = ContainerName;

            AzureStorageUtils blobUtils;
            bool needsUniqueName;
            do
            {
                blobUtils = new AzureStorageUtils(AccountName, AccountKey, versionedContainerName);
                if (await blobUtils.CheckIfContainerExistsAsync())
                {
                    versionedContainerName = $"{ContainerName}-{++version}";
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
