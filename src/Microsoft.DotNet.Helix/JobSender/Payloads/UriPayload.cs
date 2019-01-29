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

        public Task<string> UploadAsync(IBlobContainer payloadContainer, Action<string> log)
        {
            return Task.FromResult(_payloadUri.AbsoluteUri);
        }

        public Task<Tuple<string, string>> UploadAsync(IBlobContainer payloadContainer, string destination, Action<string> log)
        {
            return Task.FromResult(new Tuple<string, string>(UploadAsync(payloadContainer, log).GetAwaiter().GetResult(), destination));
        }
    }
}
