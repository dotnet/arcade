// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !DOTNET_BUILD_SOURCE_ONLY
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Azure.Identity;
using System;

namespace Microsoft.Arcade.Common
{
    public static class AuthenticationHelpers
    {
        public static string GenerateDelegationSas(string uri)
        {
            // Parse the URI to get the storage account name and container name
            var parsedUri = new Uri(uri);
            string storageAccountName = parsedUri.Host.Split('.')[0];
            if (parsedUri.Segments.Length < 2)
            {
                throw new Exception("Expected that the uri be in the form https://{storageAccountName}.blob.core.windows.net/{container} or https://{storageAccountName}.azureedge.net/{container}");
            }
            string containerName = parsedUri.Segments[1].Trim('/');

            // Create a BlobServiceClient using the default Azure credentials
            var blobServiceClient = new BlobServiceClient(new Uri($"https://{storageAccountName}.blob.core.windows.net"), new DefaultAzureCredential());

            var start = DateTimeOffset.UtcNow;
            var expiry = DateTimeOffset.UtcNow.AddHours(1);
            // Get the user delegation key
            var userDelegationKey = blobServiceClient.GetUserDelegationKey(start, expiry);

            // Create a BlobSasBuilder
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                Resource = "c",
                StartsOn = start,
                ExpiresOn = expiry
            };

            // Set permissions
            sasBuilder.SetPermissions(BlobContainerSasPermissions.Read | BlobContainerSasPermissions.List);

            // Generate the SAS token
            string sasToken = sasBuilder.ToSasQueryParameters(userDelegationKey, storageAccountName).ToString();

            return sasToken;
        }
    }
}
#else
namespace Microsoft.Arcade.Common
{
    public static class AuthenticationHelpers
    {
        public static string GenerateDelegationSas(string uri)
        {
            throw NotImplementedException("Not supported in source-only modes");
        }
    }
}
#endif
