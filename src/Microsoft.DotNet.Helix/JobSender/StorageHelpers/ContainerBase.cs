// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;

namespace Microsoft.DotNet.Helix.Client
{
    internal abstract class ContainerBase : IBlobContainer
    {
        protected abstract (BlobClient blob, string sasToken) GetBlob(string blobName);

        public async Task<Uri> UploadFileAsync(Stream stream, string blobName, Action<string> log, CancellationToken cancellationToken)
        {
            var (pageBlob, sasToken) = GetBlob(blobName);

            try
            {
                await pageBlob.UploadAsync(stream, cancellationToken);
            }
            catch (RequestFailedException e) when (e.Status == 409)
            {
                if (!pageBlob.Exists())
                {
                    log?.Invoke($"error : Upload of {pageBlob.Uri} failed with {e.ErrorCode}, but the blob does not exist.");
                    throw;
                }
                else
                {
                    log?.Invoke($"warning : Upload of {pageBlob.Uri} failed with {e.ErrorCode}. The blob exists, continuing");
                }
            }

            return new UriBuilder(pageBlob.Uri) { Query = sasToken }.Uri;
        }

        public async Task<Uri> UploadTextAsync(string text, string blobName, Action<string> log, CancellationToken cancellationToken)
        {
            var (pageBlob, sasToken) = GetBlob(blobName);
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            return await UploadFileAsync(new MemoryStream(bytes), blobName, log, cancellationToken);
        }

        public abstract string Uri { get; }
        public abstract string ReadSas { get; }
        public abstract string WriteSas { get; }
    }
}
