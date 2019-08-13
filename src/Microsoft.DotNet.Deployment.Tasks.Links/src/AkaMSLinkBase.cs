// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Microsoft.DotNet.Deployment.Tasks.Links
{
    public abstract class AkaMSLinkBase : Microsoft.Build.Utilities.Task
    {
        private const string ApiBaseUrl = "https://redirectionapi.trafficmanager.net/api/aka";
        private const string Endpoint = "https://microsoft.onmicrosoft.com/redirectionapi";
        private const string Authority = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/authorize";

        [Required]
        // Authentication data
        public string ClientId { get; set; }
        [Required]
        // Authentication data
        public string ClientSecret { get; set; }
        [Required]
        public string Tenant { get; set; }
        protected string apiTargetUrl => $"{ApiBaseUrl}/1/{Tenant}";

        protected HttpClient GetClient()
        {
#if NETCOREAPP
            var platformParameters = new PlatformParameters();
#elif NETFRAMEWORK
            var platformParameters = new PlatformParameters(PromptBehavior.Auto);
#else
#error "Unexpected TFM"
#endif
            AuthenticationContext authContext = new AuthenticationContext(Authority);
            ClientCredential credential = new ClientCredential(ClientId, ClientSecret);
            AuthenticationResult token = authContext.AcquireTokenAsync(Endpoint, credential).Result;

            HttpClient httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true });
            httpClient.DefaultRequestHeaders.Add("Authorization", token.CreateAuthorizationHeader());

            return httpClient;
        }

    }
}
