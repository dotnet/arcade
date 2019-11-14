using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.DotNet.Helix.Client
{

    internal abstract class ContainerBase : IBlobContainer
    {
        protected abstract (CloudBlockBlob blob, string sasToken) GetBlob(string blobName);

        public async Task<Uri> UploadFileAsync(Stream stream, string blobName, CancellationToken cancellationToken)
        {
            var (pageBlob, sasToken) = GetBlob(blobName);

            await pageBlob.UploadFromStreamAsync(stream);

            return new UriBuilder(pageBlob.Uri) { Query = sasToken }.Uri;
        }

        public async Task<Uri> UploadTextAsync(string text, string blobName, CancellationToken cancellationToken)
        {
            var (pageBlob, sasToken) = GetBlob(blobName);
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await pageBlob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);

            return new UriBuilder(pageBlob.Uri) { Query = sasToken }.Uri;
        }

        public abstract string Uri { get; }
        public abstract string ReadSas { get; }
        public abstract string WriteSas { get; }
    }
}
