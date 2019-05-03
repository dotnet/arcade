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
    static class BlobUtils
    {
        public static CloudBlockBlob GetBlockBlob(string AccountName,
            string AccountKey,
            string ContainerName,
            string destinationBlob)
        {
            CloudBlobClient cloudBlobClient = new CloudStorageAccount(new StorageCredentials(AccountName, AccountKey), true).CreateCloudBlobClient();

            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(ContainerName);

            return cloudBlobContainer.GetBlockBlobReference(destinationBlob);
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

        public static async Task UploadBlockBlobAsync(
            string AccountName,
            string AccountKey,
            string ContainerName,
            string filePath,
            string destinationBlob)
        {
            destinationBlob = destinationBlob.Replace("\\", "/");

            CloudBlockBlob cloudBlockBlob = BlobUtils.GetBlockBlob(AccountName, AccountKey, ContainerName, destinationBlob);

            await cloudBlockBlob.UploadFromFileAsync(filePath);
        }

        public static bool IsFileIdenticalToBlob(string AccountName,
            string AccountKey,
            string ContainerName,
            string destinationBlob,
            string localFileFullPath)
        {
            CloudBlockBlob blobReference = GetBlockBlob(AccountName, AccountKey, ContainerName, destinationBlob);

            return IsFileIdenticalToBlob(localFileFullPath, blobReference);
        }

        /// <summary>
        /// Return a bool indicating whether a local file content is same as 
        /// the content of the pointed blob. If the blob has the ContentMD5
        /// property set the comparison is performed uniquely using that.
        /// Otherwise a byte-per-byte comparison with the content of the file
        /// is performed.
        /// </summary>
        public static bool IsFileIdenticalToBlob(string localFileFullPath, CloudBlockBlob blobReference)
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
    }
}
