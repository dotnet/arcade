// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
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
    public class AkaMSLinkManager
    {
        private const string ApiBaseUrl = "https://redirectionapi.trafficmanager.net/api/aka";
        private const string Endpoint = "https://microsoft.onmicrosoft.com/redirectionapi";
        private const string Authority = "https://login.microsoftonline.com/72f988bf-86f1-41af-91ab-2d7cd011db47/oauth2/authorize";
        /// <summary>
        ///     Aka.ms max links per batch request. There are two maximums:
        ///         - Number of links per batch (300)
        ///         - Max content size per request (50k)
        ///     It's really easy to go over 50k after content encoding is done if the
        ///     maximum number of links per requests is reached. So we limit the max size
        ///     to 100 which is typically ~70% of the overall allowable size. This has plenty of
        ///     breathing room if the link targets were to get a lot larger.
        /// </summary>
        private const int BulkApiBatchSize = 100;
        private const int MaxRetries = 5;
        private string _clientId;
        private string _clientSecret;
        private string _tenant;
        private string ApiTargeturl { get => $"{ApiBaseUrl}/1/{_tenant}"; }
        private ExponentialRetry RetryHandler;

        private Microsoft.Build.Utilities.TaskLoggingHelper _log;

        public AkaMSLinkManager(string clientId, string clientSecret, string tenant, Microsoft.Build.Utilities.TaskLoggingHelper log)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
            _tenant = tenant;
            _log = log;

            RetryHandler = new ExponentialRetry
            {
                MaxAttempts = MaxRetries
            };
        }

        /// <summary>
        /// Delete one or more aka.ms links
        /// </summary>
        /// <param name="linksToDelete">Links to delete. Should not be prefixed with 'aka.ms'</param>
        /// <returns>Async task</returns>
        public async Task DeleteLinksAsync(List<string> linksToDelete)
        {
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

                            bool success = await RetryHandler.RunAsync(async attempt =>
                            {
                                try
                                {
                                    // Use the individual deletion API. The bulk APIs only work for up to 300 items per call.
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
                                }
                                catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                                {
                                    // Avoid failing in these cases.  We could have a timeout or other failure that
                                    // doesn't show up as a normal response status code. The case we typically see is
                                    // a client timeout.
                                    _log.LogMessage(MessageImportance.High, $"Failed to delete aka.ms/{link}: {e.Message}");
                                    return false;
                                }
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
        public async Task CreateOrUpdateLinksAsync(IEnumerable<AkaMSLink> links, string linkOwners,
            string linkCreatedOrUpdatedBy, string linkGroupOwner, bool overwrite)
        {
            // Batch these up by the max batch size
            List<IEnumerable<AkaMSLink>> linkBatches = new List<IEnumerable<AkaMSLink>>();
            IEnumerable<AkaMSLink> remainingLinks = links;
            while (remainingLinks.Any())
            {
                linkBatches.Add(remainingLinks.Take(BulkApiBatchSize));
                remainingLinks = remainingLinks.Skip(BulkApiBatchSize);
            }

            await Task.WhenAll(linkBatches.Select(async batch =>
                await CreateOrUpdateLinkBatchAsync(batch, linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, overwrite, false)));
        }

        /// <summary>
        /// Bucket links by whether they exist or not.
        /// </summary>
        /// <param name="links">Links to bucket.</param>
        /// <returns>Tuple of links to create and links to update.</returns>
        private async Task<(IEnumerable<AkaMSLink> linksToCreate, IEnumerable<AkaMSLink> linksToUpdate)> BucketLinksAsync(
            IEnumerable<AkaMSLink> links)
        {
            ConcurrentBag<AkaMSLink> linksToCreate = new ConcurrentBag<AkaMSLink>();
            ConcurrentBag<AkaMSLink> linksToUpdate = new ConcurrentBag<AkaMSLink>();

            using (HttpClient client = CreateClient())
            using (var clientThrottle = new SemaphoreSlim(8, 8))
            {
                await Task.WhenAll(links.Select(async link =>
                {
                    try
                    {
                        await clientThrottle.WaitAsync();

                        bool success = await RetryHandler.RunAsync(async attempt =>
                        {
                            try
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
                                        _log.LogMessage(MessageImportance.High, $"Failed to check aka.ms/{link.ShortUrl}: {response.Content.ReadAsStringAsync().Result}");
                                        return false;
                                }

                                return true;
                            }
                            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                            {
                                // Avoid failing in these cases.  We could have a timeout or other failure that
                                // doesn't show up as a normal response status code. The case we typically see is
                                // a client timeout.
                                _log.LogMessage(MessageImportance.High, $"Failed to check aka.ms/{link.ShortUrl}: {e.Message}");
                                return false;
                            }
                        });
                    }
                    finally
                    {
                        clientThrottle.Release();
                    }
                }));
            }

            return (linksToCreate, linksToUpdate);
        }

        /// <summary>
        /// Create or update a batch of links.
        /// </summary>
        /// <param name="links">Set of links to create or update</param>
        /// <param name="linkCreatedOrUpdatedBy">The alias of the link creator. Must be valid</param>
        /// <param name="linkGroupOwner">SG owner of the link</param>
        /// <param name="linkOwners">Semicolon delimited list of link owners.</param>
        /// <param name="update">If true, existing links will be overwritten.</param>
        /// <param name="bucketed">Are these links already bucketed?</param>
        /// <returns>Async task</returns>
        private async Task CreateOrUpdateLinkBatchAsync(IEnumerable<AkaMSLink> links, string linkOwners,
            string linkCreatedOrUpdatedBy, string linkGroupOwner, bool update, bool bucketed)
        {
            _log.LogMessage(MessageImportance.High, $"{(update ? "Updating" : "Creating")} {links.Count()} aka.ms links.");

            using (HttpClient client = CreateClient())
            {
                string newOrUpdatedLinksJson = 
                    GetCreateOrUpdateLinkJson(linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, update, links);

                bool success = await RetryHandler.RunAsync(async attempt =>
                {
                    HttpRequestMessage requestMessage = new HttpRequestMessage(update ? HttpMethod.Put : HttpMethod.Post,
                            $"{ApiTargeturl}/bulk");
                    requestMessage.Content = new StringContent(newOrUpdatedLinksJson, Encoding.UTF8, "application/json");

                    using (requestMessage)
                    {
                        try
                        {
                            using (HttpResponseMessage response = await client.SendAsync(requestMessage))
                            {
                                // Check for auth failures on POST (401, and 403).
                                // No reason to retry here.
                                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                                    response.StatusCode == HttpStatusCode.Forbidden)
                                {
                                    _log.LogError($"Error creating/updating aka.ms links: {response.Content.ReadAsStringAsync().Result}");
                                    return true;
                                }

                                // If it's bad request, then there are a couple paths:
                                // - We're attempting to create links (always overwrite) - The error is real.
                                // - We're attempting to update links, but some have not been created yet and we haven't bucketed.
                                //   In this case, we should bucket the links into exist/non-existent and then call this method
                                //   with update true/false
                                // - We're attempting to update links and have already bucketed them. In this case, the error is real.
                                if (response.StatusCode == HttpStatusCode.BadRequest)
                                {
                                    if (update && !bucketed)
                                    {
                                        _log.LogMessage(MessageImportance.High, $"Failed to update aka.ms links: {response.StatusCode}\n" +
                                            $"{response.Content.ReadAsStringAsync().Result}. Will bucket and create+update.");

                                        (IEnumerable<AkaMSLink> linksToCreate, IEnumerable<AkaMSLink> linksToUpdate) = await BucketLinksAsync(links);

                                        if (linksToCreate.Any())
                                        {
                                            await CreateOrUpdateLinkBatchAsync(linksToCreate, linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, false, true);
                                        }
                                        if (linksToUpdate.Any())
                                        {
                                            await CreateOrUpdateLinkBatchAsync(linksToUpdate, linkOwners, linkCreatedOrUpdatedBy, linkGroupOwner, true, true);
                                        }
                                        return true;
                                    }
                                    else
                                    {
                                        _log.LogError($"Error creating/updating aka.ms links: {response.Content.ReadAsStringAsync().Result}");
                                        return true;
                                    }
                                }

                                if ((!update && response.StatusCode != HttpStatusCode.OK) ||
                                    (update && response.StatusCode != System.Net.HttpStatusCode.Accepted &&
                                        response.StatusCode != System.Net.HttpStatusCode.NoContent &&
                                        response.StatusCode != System.Net.HttpStatusCode.NotFound))
                                {
                                    _log.LogMessage(MessageImportance.High, $"Failed to create/update aka.ms links: {response.StatusCode}\n{response.Content.ReadAsStringAsync().Result}");
                                    return false;
                                }

                                return true;
                            }
                        }
                        catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
                        {
                            // Avoid failing in these cases.  We could have a timeout or other failure that
                            // doesn't show up as a normal response status code. The case we typically see is
                            // a client timeout.
                            _log.LogMessage(MessageImportance.High, $"Failed to create/update aka.ms links: {e.Message}");
                            return false;
                        }
                    }
                });

                if (!success)
                {
                    _log.LogError($"Failed to create/updating aka.ms links");
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
        private string GetCreateOrUpdateLinkJson(string linkOwners, string linkCreatedOrUpdatedBy, string linkGroupOwner,
            bool overwrite, IEnumerable<AkaMSLink> batchOfLinksToCreateOrUpdate)
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
