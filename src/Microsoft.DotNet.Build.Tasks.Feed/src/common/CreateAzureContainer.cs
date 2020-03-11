// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using System.Threading.Tasks;
using System;
using Azure.Storage.Sas;
using Azure.Storage.Blobs.Models;

namespace Microsoft.DotNet.Build.CloudTestTasks
{

    public sealed class CreateAzureContainer : AzureConnectionStringBuildTask
    {
        /// <summary>
        /// The name of the container to create.  The specified name must be in the correct format, see the
        /// following page for more info.  https://msdn.microsoft.com/en-us/library/azure/dd135715.aspx
        /// </summary>
        [Required]
        public string ContainerName { get; set; }

        /// <summary>
        /// When false, if the specified container already exists get a reference to it.
        /// When true, if the specified container already exists, fail the task.
        /// </summary>
        public bool FailIfExists { get; set; }

        /// <summary>
        /// The read-only SAS token created when ReadOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string ReadOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the read-only token should be valid.
        /// </summary>
        public int ReadOnlyTokenDaysValid { get; set; }

        /// <summary>
        /// The URI of the created container.
        /// </summary>
        [Output]
        public string StorageUri { get; set; }

        /// <summary>
        /// The write-only SAS token to create when the value of WriteOnlyTokenDaysValid is greater than zero.
        /// </summary>
        [Output]
        public string WriteOnlyToken { get; set; }

        /// <summary>
        /// The number of days for which the write-only token should be valid.
        /// </summary>
        public int WriteOnlyTokenDaysValid { get; set; }

        /// <summary>
        /// Whether the Container to be created is public or private
        /// </summary>
        public bool IsPublic { get; set; } = false;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync()
        {
            try
            {
                AzureStorageUtils blobUtils = new AzureStorageUtils(AccountName, AccountKey, ContainerName);

                if (FailIfExists && await blobUtils.CheckIfContainerExistsAsync())
                {
                    Log.LogError($"Container {ContainerName} already exists in storage account {AccountName}.");
                    return false;
                }

                PublicAccessType permissions = IsPublic ? PublicAccessType.Blob : PublicAccessType.None;

                StorageUri = await blobUtils.CreateContainerAsync(permissions);

                ReadOnlyToken = blobUtils.CreateSASToken(ReadOnlyTokenDaysValid, BlobContainerSasPermissions.Read);
                WriteOnlyToken = blobUtils.CreateSASToken(WriteOnlyTokenDaysValid, BlobContainerSasPermissions.Write);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }
    }
}
