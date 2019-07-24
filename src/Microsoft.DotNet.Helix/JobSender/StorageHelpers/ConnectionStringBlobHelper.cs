using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.DotNet.Helix.Client
{

    internal class ConnectionStringBlobHelper : IBlobHelper
    {
        private readonly string _connectionString;

        public ConnectionStringBlobHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<IBlobContainer> GetContainerAsync(string requestedName, string targetQueue)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(_connectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference(requestedName);
            await container.CreateIfNotExistsAsync();
            return new Container(container);
        }

        private class Container : ContainerBase
        {
            private readonly CloudBlobContainer _container;

            public Container(CloudBlobContainer container)
            {
                _container = container;
            }

            public override string Uri => _container.Uri.ToString();
            public override string ReadSas => _container.GetSharedAccessSignature(SasReadOnly);
            public override string WriteSas => _container.GetSharedAccessSignature(SasReadWrite);


            private SharedAccessBlobPolicy SasReadOnly
            {
                get
                {
                    return new SharedAccessBlobPolicy
                    {
                        SharedAccessExpiryTime = DateTime.UtcNow.AddDays(30),
                        Permissions = SharedAccessBlobPermissions.Read
                    };
                }
            }

            private SharedAccessBlobPolicy SasReadWrite
            {
                get
                {
                    return new SharedAccessBlobPolicy
                    {
                        SharedAccessExpiryTime = DateTime.UtcNow.AddDays(30),
                        Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.Read
                    };
                }
            }

            protected override (CloudBlockBlob blob, string sasToken) GetBlob(string blobName)
            {
                string sasToken = _container.GetSharedAccessSignature(SasReadOnly);
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
