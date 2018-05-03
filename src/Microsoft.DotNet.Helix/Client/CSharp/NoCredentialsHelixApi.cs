using System;
using System.Net.Http;

namespace Microsoft.DotNet.Helix.Client
{
    internal class NoCredentialsHelixApi : HelixApi
    {
        public NoCredentialsHelixApi(params DelegatingHandler[] handlers) : base(handlers)
        {
        }

        public NoCredentialsHelixApi(Uri baseUri, params DelegatingHandler[] handlers) : base(baseUri, handlers)
        {
        }
    }
}
