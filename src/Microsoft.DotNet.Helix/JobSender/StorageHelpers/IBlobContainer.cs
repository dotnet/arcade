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

    internal interface IBlobContainer
    {
        Task<Uri> UploadFileAsync(Stream stream, string blobName, CancellationToken cancellationToken);
        Task<Uri> UploadTextAsync(string text, string blobName, CancellationToken cancellationToken);
        string Uri { get; }
        string ReadSas { get; }
        string WriteSas { get; }
    }
}
