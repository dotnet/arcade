using System;

namespace Microsoft.DotNet.Helix.Client
{
    public static class ApiFactory
    {
        public static IHelixApi GetAuthenticated(string accessToken)
        {
            return new HelixApi(new HelixApiOptions(new HelixApiTokenCredential(accessToken)));
        }

        public static IHelixApi GetAnonymous()
        {
            return new HelixApi(new HelixApiOptions());
        }

        public static IHelixApi GetAuthenticated(string baseUri, string accessToken)
        {
            return new HelixApi(new HelixApiOptions(new Uri(baseUri), new HelixApiTokenCredential(accessToken)));
        }

        public static IHelixApi GetAnonymous(string baseUri)
        {
            return new HelixApi(new HelixApiOptions(new Uri(baseUri)));
        }
    }
}
