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
    internal interface IBlobHelper
    {
        Task<IBlobContainer> GetContainerAsync(string requestedName, string targetQueue, CancellationToken cancellationToken);
    }
}
