using Microsoft.Rest;
using System;
using System.Net.Http;

namespace Microsoft.DotNet.Helix.Client
{
    public static class ApiFactory
    {
        public static IHelixApi GetAuthenticated(string accessToken)
        {
            return new HelixApi(new TokenCredentials(accessToken, "token"));
        }

        public static IHelixApi GetAnonymous()
        {
            return new NoCredentialsHelixApi();
        }

        public static IHelixApi GetAuthenticated(string baseUri, string accessToken)
        {
            return new HelixApi(new Uri(baseUri), new TokenCredentials(accessToken, "token"));
        }

        public static IHelixApi GetAnonymous(string baseUri)
        {
            return new NoCredentialsHelixApi(new Uri(baseUri));
        }
    }
}
