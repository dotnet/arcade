using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace Microsoft.DotNet.Helix.Client
{

    internal abstract class ContainerBase : IBlobContainer
    {
        protected abstract (BlobClient blob, string sasToken) GetBlob(string blobName);

        public async Task<Uri> UploadFileAsync(Stream stream, string blobName, CancellationToken cancellationToken)
        {
            var (pageBlob, sasToken) = GetBlob(blobName);
            await pageBlob.UploadAsync(stream, cancellationToken);

            return new UriBuilder(pageBlob.Uri) { Query = sasToken }.Uri;
        }

        public async Task<Uri> UploadTextAsync(string text, string blobName, CancellationToken cancellationToken)
        {
            var (pageBlob, sasToken) = GetBlob(blobName);
            byte[] bytes = Encoding.UTF8.GetBytes(text);

            return await UploadFileAsync(new MemoryStream(bytes), blobName, cancellationToken);
        }

        public abstract string Uri { get; }
        public abstract string ReadSas { get; }
        public abstract string WriteSas { get; }
    }
}
