using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    internal class UriPayload : IPayload
    {
        private readonly Uri _payloadUri;

        public UriPayload(Uri payloadUri)
        {
            _payloadUri = payloadUri;
        }

        public Task<string> UploadAsync(IBlobContainer payloadContainer)
        {
            return Task.FromResult(_payloadUri.AbsoluteUri);
        }
    }
}
