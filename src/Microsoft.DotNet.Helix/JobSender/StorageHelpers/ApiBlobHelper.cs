using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.Rest;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.DotNet.Helix.Client
{

    internal class ApiBlobHelper : IBlobHelper
    {
        private readonly IStorage _helixApiStorage;
        private readonly IHelixApi _helixApi;

        public ApiBlobHelper(IStorage helixApiStorage)
        {
            _helixApiStorage = helixApiStorage;
            _helixApi = ((IServiceOperations<HelixApi>) helixApiStorage).Client;
        }

        public async Task<IBlobContainer> GetContainerAsync(string requestedName, string targetQueue, CancellationToken cancellationToken)
        {
            ContainerInformation info = await _helixApi.RetryAsync(
                () => _helixApiStorage.NewAsync(
                    new ContainerCreationRequest(30, requestedName, targetQueue)),
                ex => { },
                cancellationToken);
            var client = new CloudBlobClient(new Uri($"https://{info.StorageAccountName}.blob.core.windows.net/"), new StorageCredentials(info.WriteToken));
            CloudBlobContainer container = client.GetContainerReference(info.ContainerName);
            return new Container(container, info);
        }

        private class Container : ContainerBase
        {
            private readonly CloudBlobContainer _container;
            private readonly ContainerInformation _info;

            public Container(CloudBlobContainer container, ContainerInformation info)
            {
                _container = container;
                _info = info;
            }

            public override string Uri => _container.Uri.ToString();
            public override string ReadSas => _info.ReadToken;
            public override string WriteSas => _info.WriteToken;

            protected override (CloudBlockBlob blob, string sasToken) GetBlob(string blobName)
            {
                string sasToken = _info.ReadToken;
                if (sasToken.StartsWith("?"))
                {
                    sasToken = sasToken.Substring(1);
                }

                CloudBlockBlob blob = _container.GetBlockBlobReference(blobName);
                return (blob, sasToken);
            }
        }
    }
}
