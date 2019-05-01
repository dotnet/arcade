// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class UploadToAzure : AzureConnectionStringBuildTask, ICancelableTask
    {
        private static readonly CancellationTokenSource TokenSource = new CancellationTokenSource();
        private static readonly CancellationToken CancellationToken = TokenSource.Token;

        /// <summary>
        /// The name of the container to access.  The specified name must be in the correct format, see the
        /// following page for more info.  https://msdn.microsoft.com/en-us/library/azure/dd135715.aspx
        /// </summary>
        [Required]
        public string ContainerName { get; set; }

        /// <summary>
        /// An item group of files to upload.  Each item must have metadata RelativeBlobPath
        /// that specifies the path relative to ContainerName where the item will be uploaded.
        /// </summary>
        [Required]
        public ITaskItem[] Items { get; set; }

        /// <summary>
        /// Indicates if the destination blob should be overwritten if it already exists.  The default if false.
        /// </summary>
        public bool Overwrite { get; set; } = false;

        /// <summary>
        /// Enables idempotency when Overwrite is false.
        /// 
        /// false: (default) Attempting to upload an item that already exists fails.
        /// 
        /// true: When an item already exists, download the existing blob to check if it's
        /// byte-for-byte identical to the one being uploaded. If so, pass. If not, fail.
        /// </summary>
        public bool PassIfExistingItemIdentical { get; set; }

        /// <summary>
        /// Specifies the maximum number of clients to concurrently upload blobs to azure
        /// </summary>
        public int MaxClients { get; set; } = 8;

        public int UploadTimeoutInMinutes { get; set; } = 5;

        public void Cancel()
        {
            TokenSource.Cancel();
        }

        public override bool Execute()
        {
            return ExecuteAsync(CancellationToken).GetAwaiter().GetResult();
        }

        public async Task<bool> ExecuteAsync(CancellationToken ct)
        {
            if (Items.Length == 0)
            {
                Log.LogError("No items were provided for upload.");
                return false;
            }

            if (!CloudStorageAccount.TryParse(ConnectionString, out CloudStorageAccount storageAccount))
            {
                Log.LogError("Invalid connection string was provided.");
                return false;
            }

            Log.LogMessage("Begin uploading blobs to Azure account {0} in container {1}.",
                AccountName,
                ContainerName);

            try
            {
                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(ContainerName);

                foreach (var item in Items)
                {
                    string relativeBlobPath = item.GetMetadata("RelativeBlobPath");
                    if (string.IsNullOrEmpty(relativeBlobPath))
                        throw new Exception(string.Format("Metadata 'RelativeBlobPath' is missing for item '{0}'.", item.ItemSpec));

                    if (!File.Exists(item.ItemSpec))
                        throw new Exception(string.Format("The file '{0}' does not exist.", item.ItemSpec));

                    var blobReference = cloudBlobContainer.GetBlockBlobReference(relativeBlobPath);

                    if (!Overwrite && await blobReference.ExistsAsync())
                    {
                        if (PassIfExistingItemIdentical)
                        {
                            var localMD5 = CalculateMD5(item.ItemSpec);

                            if (blobReference.Properties.ContentMD5.Equals(localMD5, StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                        }

                        throw new Exception(string.Format("The blob '{0}' already exists.", relativeBlobPath));
                    }

                    await blobReference.UploadFromFileAsync(item.ItemSpec);
                }

                Log.LogMessage("Upload to Azure is complete, a total of {0} items were uploaded.", Items.Length);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
            return !Log.HasLoggedErrors;
        }

        static string CalculateMD5(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
        }
    }
}
