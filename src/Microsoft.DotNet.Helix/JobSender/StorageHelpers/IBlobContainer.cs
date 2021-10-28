using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{

    internal interface IBlobContainer
    {
        Task<Uri> UploadFileAsync(Stream stream, string blobName, CancellationToken cancellationToken);
        Task<Uri> UploadTextAsync(string text, string blobName, CancellationToken cancellationToken);
        string Uri { get; }
        string ReadSas { get; }
        string WriteSas { get; }
    }
}
