// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using Microsoft.Azure.Storage.Blob;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class AzureStorageUtils
    {
        public CloudBlobContainer Container { get; set; }

        public AzureStorageUtils(string AccountName, string AccountKey, string ContainerName)
        {
            StorageCredentials credentials = new StorageCredentials(AccountName, AccountKey);
            CloudStorageAccount storageAccount = new CloudStorageAccount(credentials, true);
            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();

            Container = cloudBlobClient.GetContainerReference(ContainerName);
        }

        public CloudBlockBlob GetBlockBlob(string destinationBlob)
        {
            return Container.GetBlockBlobReference(destinationBlob);
        }

        public static string CalculateMD5(string filename)
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

        public async Task UploadBlockBlobAsync(string filePath, string blobPath)
        {
            CloudBlockBlob cloudBlockBlob = GetBlockBlob(blobPath.Replace("\\", "/"));

            await cloudBlockBlob.UploadFromFileAsync(filePath);
        }

        public async Task<bool> IsFileIdenticalToBlobAsync(string blobPath, string localFileFullPath)
        {
            CloudBlockBlob blobReference = GetBlockBlob(blobPath);

            return await IsFileIdenticalToBlob(localFileFullPath, blobReference);
        }

        /// <summary>
        /// Return a bool indicating whether a local file content is same as 
        /// the content of the pointed blob. If the blob has the ContentMD5
        /// property set the comparison is performed uniquely using that.
        /// Otherwise a byte-per-byte comparison with the content of the file
        /// is performed.
        /// </summary>
        public async Task<bool> IsFileIdenticalToBlob(string localFileFullPath, CloudBlockBlob blobReference)
        {
            blobReference.FetchAttributes();

            if (!string.IsNullOrEmpty(blobReference.Properties.ContentMD5))
            {
                var localMD5 = CalculateMD5(localFileFullPath);
                var blobMD5 = blobReference.Properties.ContentMD5;

                return blobMD5.Equals(localMD5, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                int OneMegaBytes = 1 * 1024 * 1024;

                if (blobReference.Properties.Length < OneMegaBytes) {
                    byte[] existingBytes = new byte[blobReference.Properties.Length];
                    byte[] localBytes = File.ReadAllBytes(localFileFullPath);

                    blobReference.DownloadToByteArray(existingBytes, 0);

                    return localBytes.SequenceEqual(existingBytes);
                }
                else
                {
                    using (Stream localFileStream = File.OpenRead(localFileFullPath))
                    using (Stream blobStream = await blobReference.OpenReadAsync())
                    {
                        byte[] localBuffer = new byte[OneMegaBytes];
                        byte[] remoteBuffer = new byte[OneMegaBytes];
                        int bytesLocalFile = 0;

                        do
                        {
                            bytesLocalFile = await blobStream.ReadAsync(remoteBuffer, 0, OneMegaBytes);
                            int bytesBlobFile = await localFileStream.ReadAsync(localBuffer, 0, OneMegaBytes);

                            if ((bytesLocalFile != bytesBlobFile) || !remoteBuffer.SequenceEqual(localBuffer))
                            {
                                return false;
                            }
                        }
                        while (bytesLocalFile > 0);

                        return true;
                    }
                }
            }
        }

        public async Task<string> CreateContainerAsync(BlobContainerPermissions permissions)
        {
            await Container.CreateIfNotExistsAsync();
            await Container.SetPermissionsAsync(permissions);

            return Container.Uri.ToString();
        }

        public string CreateSASToken(int tokenExpirationInDays, SharedAccessBlobPermissions containerPermissions)
        {
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTimeOffset.UtcNow.AddDays(tokenExpirationInDays),
                Permissions = containerPermissions
            };

            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            return Container.GetSharedAccessSignature(sasConstraints);
        }

        public async Task<bool> CheckIfContainerExistsAsync()
        {
            return await Container.ExistsAsync();
        }

        public async Task<bool> CheckIfBlobExistsAsync(string blobPath)
        {
            var blob = GetBlockBlob(blobPath);

            return await blob.ExistsAsync();
        }
    }
}
