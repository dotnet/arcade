// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Deployment.Tasks.Links.src;
using Microsoft.DotNet.VersionTools.BuildManifest;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class LatestLinksManager
    {
        private TaskLoggingHelper Logger { get; }
        private AkaMSLinkManager LinkManager { get; } = null;
        private string AkaMSClientId { get; }
        private string AkaMSClientSecret { get; }
        private string AkaMSTenant { get; }
        private string AkaMsOwners { get; }
        private string AkaMSCreatedBy { get; }
        private string AkaMSGroupOwner { get; }

        private static HashSet<string> AccountsWithCdns { get; } = new()
        {
            "dotnetcli.blob.core.windows.net", "dotnetbuilds.blob.core.windows.net",
        };

        public LatestLinksManager(
            string akaMSClientId,
            string akaMSClientSecret,
            string akaMSTenant,
            string akaMSGroupOwner,
            string akaMSCreatedBy,
            string akaMsOwners,
            TaskLoggingHelper logger)
        {
            Logger = logger;
            AkaMSClientId = akaMSClientId;
            AkaMSClientSecret = akaMSClientSecret;
            AkaMSTenant = akaMSTenant;
            AkaMSGroupOwner = akaMSGroupOwner;
            AkaMSCreatedBy = akaMSCreatedBy;
            AkaMsOwners = akaMsOwners;
            LinkManager = new AkaMSLinkManager(AkaMSClientId, AkaMSClientSecret, AkaMSTenant, Logger);
        }

        public async System.Threading.Tasks.Task CreateOrUpdateLatestLinksAsync(
            HashSet<string> assetsToPublish,
            TargetFeedConfig feedConfig,
            int expectedSuffixLength)
        {
            if (string.IsNullOrEmpty(feedConfig.LatestLinkShortUrlPrefix))
            {
                return;
            }

            string feedBaseUrl = feedConfig.SafeTargetURL;
            if (expectedSuffixLength != 0)
            {
                // Strip away the feed expected suffix (index.json)
                feedBaseUrl = feedBaseUrl.Substring(0, feedBaseUrl.Length - expectedSuffixLength);
            }
            if (!feedBaseUrl.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                feedBaseUrl += "/";
            }
            if (AccountsWithCdns.Any(account => feedBaseUrl.Contains(account)))
            {
                // The storage accounts are in a single datacenter in the US and thus download 
                // times can be painful elsewhere. The CDN helps with this therefore we point the target 
                // of the aka.ms links to the CDN.
                feedBaseUrl = feedBaseUrl.Replace(".blob.core.windows.net", ".azureedge.net");
            }

            Logger.LogMessage(MessageImportance.High, "\nThe following aka.ms links for blobs will be created:");
            IEnumerable<AkaMSLink> linksToCreate = assetsToPublish
                .Where(asset => !feedConfig.FilenamesToExclude.Contains(Path.GetFileName(asset)))
                .Select(asset =>
            {

                // blob path.
                string actualTargetUrl = feedBaseUrl + asset;

                AkaMSLink newLink = new AkaMSLink
                {
                    ShortUrl = GetLatestShortUrlForBlob(feedConfig, asset, feedConfig.Flatten),
                    TargetUrl = actualTargetUrl
                };
                Logger.LogMessage(MessageImportance.High, $"  {Path.GetFileName(asset)}");

                Logger.LogMessage(MessageImportance.High, $"  aka.ms/{newLink.ShortUrl} -> {newLink.TargetUrl}");

                return newLink;
            }).ToList();

            await LinkManager.CreateOrUpdateLinksAsync(linksToCreate, AkaMsOwners, AkaMSCreatedBy, AkaMSGroupOwner, true);
        }

        /// <summary>
        ///     Get the short url for a blob.
        /// </summary>
        /// <param name="feedConfig">Feed configuration</param>
        /// <param name="blob">Blob</param>
        /// <returns>Short url prefix for the blob.</returns>
        public string GetLatestShortUrlForBlob(TargetFeedConfig feedConfig, string asset, bool flatten)
        {
            string blobIdWithoutVersions = VersionIdentifier.RemoveVersions(asset);

            if (flatten)
            {
                blobIdWithoutVersions = Path.GetFileName(blobIdWithoutVersions);
            }

            return Path.Combine(feedConfig.LatestLinkShortUrlPrefix, blobIdWithoutVersions).Replace("\\", "/");
        }
    }
}
