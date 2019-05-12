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
    public class BlobUtils
    {
        public CloudBlobContainer Container { get; set; }

        public BlobUtils(string AccountName, string AccountKey, string ContainerName)
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

        public bool IsFileIdenticalToBlob(string blobPath, string localFileFullPath)
        {
            CloudBlockBlob blobReference = GetBlockBlob(blobPath);

            return IsFileIdenticalToBlob(localFileFullPath, blobReference);
        }

        /// <summary>
        /// Return a bool indicating whether a local file content is same as 
        /// the content of the pointed blob. If the blob has the ContentMD5
        /// property set the comparison is performed uniquely using that.
        /// Otherwise a byte-per-byte comparison with the content of the file
        /// is performed.
        /// </summary>
        public bool IsFileIdenticalToBlob(string localFileFullPath, CloudBlockBlob blobReference)
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
                byte[] existingBytes = new byte[blobReference.Properties.Length];
                byte[] localBytes = File.ReadAllBytes(localFileFullPath);

                blobReference.DownloadToByteArray(existingBytes, 0);

                return localBytes.SequenceEqual(existingBytes);
            }
        }

        public async Task<bool> CheckIfBlobExistsAsync(string blobPath)
        {
            var blob = GetBlockBlob(blobPath);

            return await blob.ExistsAsync();
        }
    }
}
