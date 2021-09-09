// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public static class AzureStorageExtensions
    {
        public static string CalculateMD5(string filename)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(filename);
            byte[] hash = md5.ComputeHash(stream);
            return Convert.ToBase64String(hash);
        }

        public static async Task<bool> IsFileIdenticalToBlobAsync(this BlobClient client, string file)
        {
            BlobProperties properties = await client.GetPropertiesAsync();
            if (properties.ContentHash != null)
            {
                var localMD5 = CalculateMD5(file);
                var blobMD5 = Convert.ToBase64String(properties.ContentHash);
                return blobMD5.Equals(localMD5, StringComparison.OrdinalIgnoreCase);
            }

            int bytesPerMegabyte = 1 * 1024 * 1024;
            if (properties.ContentLength < bytesPerMegabyte)
            {
                byte[] existingBytes = new byte[properties.ContentLength];
                byte[] localBytes = File.ReadAllBytes(file);

                using var stream = new MemoryStream(existingBytes, true);
                await client.DownloadToAsync(stream).ConfigureAwait(false);
                return localBytes.SequenceEqual(existingBytes);
            }

            using Stream localFileStream = File.OpenRead(file);
            byte[] localBuffer = new byte[bytesPerMegabyte];
            byte[] remoteBuffer = new byte[bytesPerMegabyte];
            int localBytesRead = 0;

            do
            {
                long start = localFileStream.Position;
                localBytesRead = await localFileStream.ReadAsync(localBuffer, 0, bytesPerMegabyte);

                var range = new HttpRange(start, localBytesRead);
                BlobDownloadInfo download = await client.DownloadAsync(range).ConfigureAwait(false);
                if (download.ContentLength != localBytesRead)
                {
                    return false;
                }

                using (var stream = new MemoryStream(remoteBuffer, true))
                {
                    await download.Content.CopyToAsync(stream).ConfigureAwait(false);
                }
                if (!remoteBuffer.SequenceEqual(localBuffer))
                {
                    return false;
                }
            }
            while (localBytesRead > 0);

            return true;
        }
    }
}
