// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.DotNet.VersionTools.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.DotNet.Deployment.Tasks.Links.src
{
    /// <summary>
    ///     A single aka.ms link.
    /// </summary>
    public class AkaMSLink
    {
        /// <summary>
        /// Target of the link
        /// </summary>
        public string TargetUrl { get; set; }
        /// <summary>
        /// Short url of the link. Should only include the fragment element of the url, not the full aka.ms
        /// link.
        /// </summary>
        public string ShortUrl { get; set; }
        /// <summary>
        /// Description of the link.
        /// </summary>
        public string Description { get; set; } = "";
    }

    public class AkaMSLinkManager
    {
        private const string ApiBaseUrl = "https://redirectionapi.trafficmanager.net/api/aka";
        private const string Endpoint = "https://microsoft.onmicrosoft.com/redirectionapi";
        private const string Authority = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/authorize";
        private const int BulkApiBatchSize = 300;

        private string _clientId;
        private string _clientSecret;
        private string _tenant;
        private string ApiTargeturl { get => $"{ApiBaseUrl}/1/{_tenant}"; }

        private Microsoft.Build.Utilities.TaskLoggingHelper _log;

        public AkaMSLinkManager(string clientId, string clientSecret, string tenant, Microsoft.Build.Utilities.TaskLoggingHelper log)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _tenant = tenant;
            _log = log;
        }

        /// <summary>
        /// Delete one or more aka.ms links
        /// </summary>
        /// <param name="linksToDelete">Links to delete. Should not be prefixed with 'aka.ms'</param>
        /// <returns>Async task</returns>
        public async Task DeleteLinksAsync(List<string> linksToDelete)
        {
            var retryHandler = new ExponentialRetry
            {
                MaxAttempts = 5
            };

            // The bulk hard-delete APIs do not have short-url forms (only identity), so they must be
            // deleted individually. Use a semaphore to avoid excessive numbers of concurrent API calls

            using (HttpClient client = CreateClient())
            {
                using (var clientThrottle = new SemaphoreSlim(8, 8))
                {
                    await Task.WhenAll(linksToDelete.Select(async link =>
                    {
                        try
                        {
                            await clientThrottle.WaitAsync();
                            
                            bool success = await retryHandler.RunAsync(async attempt =>
                            {
                                // Use the bulk deletion API. The bulk APIs only work for up to 300 items per call.
                                // So batch
                                var response = await client.DeleteAsync($"{ApiTargeturl}/harddelete/{link}");

                                // 400, 401, and 403 indicate auth failure or bad requests that should not be retried.
                                // Check for auth failures/bad request on POST (400, 401, and 403)
                                if (response.StatusCode == HttpStatusCode.BadRequest ||
                                    response.StatusCode == HttpStatusCode.Unauthorized ||
                                    response.StatusCode == HttpStatusCode.Forbidden)
                                {
                                    _log.LogError($"Error deleting aka.ms/{link}: {response.Content.ReadAsStringAsync().Result}");
                                    return true;
                                }

                                // Success if it's 202, 204, 404
                                if (response.StatusCode != System.Net.HttpStatusCode.NoContent &&
                                    response.StatusCode != System.Net.HttpStatusCode.NotFound)
                                {
                                    _log.LogMessage(MessageImportance.High, $"Failed to delete aka.ms/{link}: {response.Content.ReadAsStringAsync().Result}");
                                    return false;
                                }

                                return true;
                            });
                        }
                        finally
                        {
                            clientThrottle.Release();
                        }
                    }));
                }
            }
        }

        /// <summary>
        /// Create or update one or more links
        /// </summary>
        /// <param name="links">Set of links to create or update</param>
        /// <param name="linkCreatedOrUpdatedBy">The alias of the link creator. Must be valid</param>
        /// <param name="linkGroupOwner">SG owner of the link</param>
        /// <param name="linkOwners">Semicolon delimited list of link owners.</param>
        /// <param name="overwrite">If true, existing links will be overwritten.</param>
        /// <returns>Async task</returns>
        /// <remarks>
        /// If overwrite is false, the we use the bulk create API, which will fail when the link already
        /// exists. If overwrite is true, then we need to bucketize the links we want to create and ones
        /// we want to update, and make two separate calls.
        /// </remarks>
        public async Task CreateOrUpateLinksAsync(List<AkaMSLink> links, string linkOwners,
            string linkCreatedOrUpdatedBy, string linkGroupOwner, bool overwrite)
        {
            // Bucketize the links if necessary, then call the implementation.
            if (!overwrite)
            {
                await CreateOrUpateLinksImplAsync(links, linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, overwrite);
                return;
            }

            var retryHandler = new ExponentialRetry
            {
                MaxAttempts = 5
            };

            ConcurrentBag<AkaMSLink> linksToCreate = new ConcurrentBag<AkaMSLink>();
            ConcurrentBag<AkaMSLink> linksToUpdate = new ConcurrentBag<AkaMSLink>();

            using (HttpClient client = CreateClient())
            {
                using (var clientThrottle = new SemaphoreSlim(8, 8))
                {
                    await Task.WhenAll(links.Select(async link =>
                    {
                        try
                        {
                            await clientThrottle.WaitAsync();

                            bool success = await retryHandler.RunAsync(async attempt =>
                            {
                                // Use the bulk deletion API. The bulk APIs only work for up to 300 items per call.
                                // So batch
                                var response = await client.GetAsync($"{ApiTargeturl}/{link.ShortUrl}");

                                // 401, and 403 indicate auth failures that should not be retried.
                                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                                    response.StatusCode == HttpStatusCode.Forbidden)
                                {
                                    _log.LogError($"Error getting aka.ms/{link.ShortUrl}: {response.Content.ReadAsStringAsync().Result}");
                                    return true;
                                }

                                // If 200, then the link should be updated, if 400, then it should be
                                // created
                                switch (response.StatusCode)
                                {
                                    case HttpStatusCode.OK:
                                        linksToUpdate.Add(link);
                                        break;
                                    case HttpStatusCode.NotFound:
                                        linksToCreate.Add(link);
                                        break;
                                    default:
                                        _log.LogMessage(MessageImportance.High, $"Failed to delete aka.ms/{link.ShortUrl}: {response.Content.ReadAsStringAsync().Result}");
                                        return false;
                                }

                                return true;
                            });
                        }
                        finally
                        {
                            clientThrottle.Release();
                        }
                    }));
                }
            }

            if (linksToCreate.Any())
            {
                await CreateOrUpateLinksImplAsync(linksToCreate.ToList(), linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, false);
            }
            if (linksToUpdate.Any())
            {
                await CreateOrUpateLinksImplAsync(linksToUpdate.ToList(), linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, true);
            }
        }

        /// <summary>
        /// Create or update one or more links
        /// </summary>
        /// <param name="links">Set of links to create or update</param>
        /// <param name="linkCreatedOrUpdatedBy">The alias of the link creator. Must be valid</param>
        /// <param name="linkGroupOwner">SG owner of the link</param>
        /// <param name="linkOwners">Semicolon delimited list of link owners.</param>
        /// <param name="overwrite">If true, existing links will be overwritten.</param>
        /// <returns>Async task</returns>
        private async Task CreateOrUpateLinksImplAsync(List<AkaMSLink> links, string linkOwners,
            string linkCreatedOrUpdatedBy, string linkGroupOwner, bool overwrite)
        {
            var retryHandler = new ExponentialRetry
            {
                MaxAttempts = 5
            };

            _log.LogMessage(MessageImportance.High, $"{(overwrite ? "Creating" : "Updating")} {links.Count} aka.ms links.");

            using (HttpClient client = CreateClient())
            {
                // The links should be divided into BulkApiBatchSize element chunks
                var currentElement = 0;
                var batchOfLinksToCreateOrUpdate = links.Skip(currentElement).Take(BulkApiBatchSize).ToList();

                while (batchOfLinksToCreateOrUpdate.Count > 0)
                {
                    string newOrUpdatedLinksJson = 
                        GetCreateOrUpdateLinkJson(linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, overwrite, batchOfLinksToCreateOrUpdate);

                    bool success = await retryHandler.RunAsync(async attempt =>
                    {
                        HttpRequestMessage requestMessage = new HttpRequestMessage(overwrite ? HttpMethod.Put : HttpMethod.Post,
                               $"{ApiTargeturl}/bulk");
                        requestMessage.Content = new StringContent(newOrUpdatedLinksJson, Encoding.UTF8, "application/json");

                        using (requestMessage)
                        {
                            using (HttpResponseMessage response = await client.SendAsync(requestMessage))
                            {
                                // Check for auth failures/bad request on POST (400, 401, and 403).
                                // No reason to retry here.
                                if (response.StatusCode == HttpStatusCode.BadRequest ||
                                    response.StatusCode == HttpStatusCode.Unauthorized ||
                                    response.StatusCode == HttpStatusCode.Forbidden)
                                {
                                    _log.LogError($"Error creating/updating aka.ms links: {response.Content.ReadAsStringAsync().Result}");
                                    return true;
                                }

                                if ((!overwrite && response.StatusCode != HttpStatusCode.OK) ||
                                    (overwrite && response.StatusCode != System.Net.HttpStatusCode.Accepted &&
                                     response.StatusCode != System.Net.HttpStatusCode.NoContent &&
                                     response.StatusCode != System.Net.HttpStatusCode.NotFound))
                                {
                                    _log.LogMessage(MessageImportance.High, $"Failed to create/update aka.ms links: {response.StatusCode}\n{response.Content.ReadAsStringAsync().Result}");
                                    return false;
                                }

                                return true;
                            }
                        }
                    });

                    if (!success)
                    {
                        _log.LogError($"Failed to create/updating aka.ms links");
                    }

                    currentElement += BulkApiBatchSize;
                    batchOfLinksToCreateOrUpdate = links.Skip(currentElement).Take(BulkApiBatchSize).ToList();
                }
            }
        }

        /// <summary>
        /// Get the json needed to create or update links.
        /// </summary>
        /// <param name="linkOwners">Link owners. Semicolon delimited list of aliases</param>
        /// <param name="linkCreatedOrUpdatedBy">Aliases of link creator and updator</param>
        /// <param name="linkGroupOwner">Alias of group owner. Can be empty</param>
        /// <param name="overwrite">If true, overwrite existing links, otherwise fail if they already exist.</param>
        /// <param name="batchOfLinksToCreateOrUpdate">Links to create/update</param>
        /// <returns>String representation of the link creation content</returns>
        private string GetCreateOrUpdateLinkJson(string linkOwners, string linkCreatedOrUpdatedBy, string linkGroupOwner, bool overwrite, List<AkaMSLink> batchOfLinksToCreateOrUpdate)
        {
            if (overwrite)
            {
                return JsonConvert.SerializeObject(batchOfLinksToCreateOrUpdate.Select(link =>
                {
                    return new
                    {
                        shortUrl = link.ShortUrl,
                        owners = linkOwners,
                        targetUrl = link.TargetUrl,
                        lastModifiedBy = linkCreatedOrUpdatedBy,
                        description = link.Description,
                        groupOwner = linkGroupOwner
                    };
                }));
            }
            else
            {
                return JsonConvert.SerializeObject(batchOfLinksToCreateOrUpdate.Select(link =>
                {
                    return new
                    {
                        shortUrl = link.ShortUrl,
                        owners = linkOwners,
                        targetUrl = link.TargetUrl,
                        lastModifiedBy = linkCreatedOrUpdatedBy,
                        description = link.Description,
                        groupOwner = linkGroupOwner,
                        // Create specific items
                        createdBy = linkCreatedOrUpdatedBy,
                        isVanity = !string.IsNullOrEmpty(link.ShortUrl)
                    };
                }));
            }
        }

        private HttpClient CreateClient()
        {
#if NETCOREAPP
            var platformParameters = new PlatformParameters();
#elif NETFRAMEWORK
            var platformParameters = new PlatformParameters(PromptBehavior.Auto);
#else
#error "Unexpected TFM"
#endif
            AuthenticationContext authContext = new AuthenticationContext(Authority);
            ClientCredential credential = new ClientCredential(_clientId, _clientSecret);
            AuthenticationResult token = authContext.AcquireTokenAsync(Endpoint, credential).Result;

            HttpClient httpClient = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true });
            httpClient.DefaultRequestHeaders.Add("Authorization", token.CreateAuthorizationHeader());

            return httpClient;
        }
    }
}
