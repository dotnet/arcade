// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.CloudTestTasks
{
    public class AzureStorageUtils
    {
        private readonly Dictionary<string, string> MimeMappings = new Dictionary<string, string>()
        {
            {".svg", "image/svg+xml"},
            {".version", "text/plain"}
        };

        private readonly Dictionary<string, string> CacheMappings = new Dictionary<string, string>()
        {
            {".svg", "no-cache"}
        };

        // Save the credential so we can sign SAS tokens
        private readonly StorageSharedKeyCredential _credential;

        public BlobContainerClient Container { get; set; }

        public AzureStorageUtils(string AccountName, string AccountKey, string ContainerName)
        {
            _credential = new StorageSharedKeyCredential(AccountName, AccountKey);
            Uri endpoint = new Uri($"https://{AccountName}.blob.core.windows.net");
            BlobServiceClient service = new BlobServiceClient(endpoint, _credential);
            Container = service.GetBlobContainerClient(ContainerName);
        }

        public BlobClient GetBlob(string destinationBlob) =>
            Container.GetBlobClient(destinationBlob);

        public BlockBlobClient GetBlockBlob(string destinationBlob) =>
            Container.GetBlockBlobClient(destinationBlob);

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
            BlobClient blob = GetBlob(blobPath.Replace("\\", "/"));
            BlobHttpHeaders headers = GetBlobHeadersByExtension(filePath);
            await blob.UploadAsync(
                filePath,
                headers)
                .ConfigureAwait(false);
        }

        public async Task<bool> IsFileIdenticalToBlobAsync(string localFileFullPath, string blobPath) =>
            await IsFileIdenticalToBlobAsync(localFileFullPath, GetBlob(blobPath)).ConfigureAwait(false);

        /// <summary>
        /// Return a bool indicating whether a local file's content is the same as 
        /// the content of a given blob. 
        /// 
        /// If the blob has the ContentHash property set, the comparison is performed using 
        /// that (MD5 hash).  All recently-uploaded blobs or those uploaded by these libraries
        /// should; some blob clients older than ~2012 may upload without the property set.
        /// 
        /// When the ContentHash property is unset, a byte-by-byte comparison is performed.
        /// </summary>
        public async Task<bool> IsFileIdenticalToBlobAsync(string localFileFullPath, BlobClient blob)
        {
            BlobProperties properties = await blob.GetPropertiesAsync();
            if (properties.ContentHash != null)
            {
                var localMD5 = CalculateMD5(localFileFullPath);
                var blobMD5 = Convert.ToBase64String(properties.ContentHash);
                return blobMD5.Equals(localMD5, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                int bytesPerMegabyte = 1 * 1024 * 1024;
                if (properties.ContentLength < bytesPerMegabyte)
                {
                    byte[] existingBytes = new byte[properties.ContentLength];
                    byte[] localBytes = File.ReadAllBytes(localFileFullPath);

                    using (MemoryStream stream = new MemoryStream(existingBytes, true))
                    {
                        await blob.DownloadToAsync(stream).ConfigureAwait(false);
                    }
                    return localBytes.SequenceEqual(existingBytes);
                }
                else
                {
                    using (Stream localFileStream = File.OpenRead(localFileFullPath))
                    {
                        byte[] localBuffer = new byte[bytesPerMegabyte];
                        byte[] remoteBuffer = new byte[bytesPerMegabyte];
                        int bytesLocalFile = 0;

                        do
                        {
                            long start = localFileStream.Position;
                            int localBytesRead = await localFileStream.ReadAsync(localBuffer, 0, bytesPerMegabyte);

                            HttpRange range = new HttpRange(start, localBytesRead);
                            BlobDownloadInfo download = await blob.DownloadAsync(range).ConfigureAwait(false);
                            if (download.ContentLength != localBytesRead)
                            {
                                return false;
                            }
                            using (MemoryStream stream = new MemoryStream(remoteBuffer, true))
                            {
                                await download.Content.CopyToAsync(stream).ConfigureAwait(false);
                            }
                            if (!remoteBuffer.SequenceEqual(localBuffer))
                            {
                                return false;
                            }
                        }
                        while (bytesLocalFile > 0);
                    }
                    return true;
                }
            }
        }

        public async Task<string> CreateContainerAsync(PublicAccessType publicAccess)
        {
            await Container.CreateIfNotExistsAsync(publicAccess).ConfigureAwait(false);
            return Container.Uri.ToString();
        }

        public string CreateSASToken(int tokenExpirationInDays, BlobContainerSasPermissions containerPermissions)
        {
            BlobSasBuilder builder = new BlobSasBuilder
            {
                BlobContainerName = Container.Name,
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(tokenExpirationInDays)
            };
            builder.SetPermissions(containerPermissions);
            return builder.ToSasQueryParameters(_credential).ToString();
        }

        public async Task<bool> CheckIfContainerExistsAsync() =>
            await Container.ExistsAsync().ConfigureAwait(false);

        public async Task<bool> CheckIfBlobExistsAsync(string blobPath) =>
            await GetBlob(blobPath).ExistsAsync().ConfigureAwait(false);


        private BlobHttpHeaders GetBlobHeadersByExtension(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("An attempt to get the MIME mapping of an empty path was made.");
            }

            var fileExtension = Path.GetExtension(filePath).ToLowerInvariant();

            BlobHttpHeaders headers = new BlobHttpHeaders
            {
                ContentType = MimeMappings.TryGetValue(fileExtension, out string cttType) ?
                cttType :
                "application/octet-stream"
            };

            if (CacheMappings.TryGetValue(fileExtension, out string cacheCtrl))
            {
                headers.CacheControl = cacheCtrl;
            }

            return headers;
        }
    }
}
