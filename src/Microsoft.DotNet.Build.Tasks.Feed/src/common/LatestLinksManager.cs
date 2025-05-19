// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Feed.Model;
using Microsoft.DotNet.Deployment.Tasks.Links;
using Microsoft.DotNet.VersionTools.BuildManifest;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class LatestLinksManager
    {
        private TaskLoggingHelper _logger { get; }
        private AkaMSLinkManager _linkManager { get; } = null;
        private string _akaMSOwners { get; }
        private string _akaMSCreatedBy { get; }
        private string _akaMSGroupOwner { get; }

        private static Dictionary<string, string> AccountsWithCdns { get; } = new()
        {
            {"dotnetcli.blob.core.windows.net", "builds.dotnet.microsoft.com" },
            {"dotnetbuilds.blob.core.windows.net", "ci.dot.net" }
        };

        public static readonly ImmutableList<Regex> AkaMSCreateLinkPatterns = new List<Regex>() {
            new Regex(@"\.rpm(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.zip(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.version(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.deb(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.gz(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.pkg(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.msi(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.exe(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.svg(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.tgz(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.jar(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"\.pom(\.sha512)?$", RegexOptions.IgnoreCase),
            new Regex(@"productcommit", RegexOptions.IgnoreCase),
            new Regex(@"productversion", RegexOptions.IgnoreCase)
        }.ToImmutableList();

        public static readonly ImmutableList<Regex> AkaMSDoNotCreateLinkPatterns = new List<Regex>() {
            new Regex(@"wixpack", RegexOptions.IgnoreCase),
        }.ToImmutableList();

        public LatestLinksManager(
            string akaMSClientId,
            string akaMSClientSecret,
            string akaMSTenant,
            string akaMSGroupOwner,
            string akaMSCreatedBy,
            string akaMsOwners,
            TaskLoggingHelper logger)
        {
            _logger = logger;
            _akaMSGroupOwner = akaMSGroupOwner;
            _akaMSCreatedBy = akaMSCreatedBy;
            _akaMSOwners = akaMsOwners;
            _linkManager = new AkaMSLinkManager(akaMSClientId, akaMSClientSecret, akaMSTenant, _logger);
        }

        public LatestLinksManager(
            string akaMSClientId,
            X509Certificate2 certificate,
            string akaMSTenant,
            string akaMSGroupOwner,
            string akaMSCreatedBy,
            string akaMsOwners,
            TaskLoggingHelper logger)
        {
            _logger = logger;
            _akaMSGroupOwner = akaMSGroupOwner;
            _akaMSCreatedBy = akaMSCreatedBy;
            _akaMSOwners = akaMsOwners;
            _linkManager = new AkaMSLinkManager(akaMSClientId, certificate, akaMSTenant, _logger);
        }

        public static string ComputeLatestLinkBase(TargetFeedConfig feedConfig)
        {
            string feedBaseUrl = feedConfig.SafeTargetURL;
            if (!feedBaseUrl.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                feedBaseUrl += "/";
            }
            var authority = new Uri(feedBaseUrl).Authority;
            if (AccountsWithCdns.TryGetValue(authority, out var replacementAuthority))
            {
                // The storage accounts are in a single datacenter in the US and thus download 
                // times can be painful elsewhere. The CDN helps with this therefore we point the target 
                // of the aka.ms links to the CDN.
                feedBaseUrl = feedBaseUrl.Replace(authority, replacementAuthority);
            }

            return feedBaseUrl;
        }

        public async System.Threading.Tasks.Task CreateOrUpdateLatestLinksAsync(
            HashSet<string> assetsToPublish,
            TargetFeedConfig feedConfig,
            int expectedSuffixLength)
        {
            // The link manager should only be used if there are actually links that could
            // be created.
            if (!feedConfig.LatestLinkShortUrlPrefixes.Any())
            {
                throw new ArgumentException("No link prefixes specified.");
            }

            string feedBaseUrl = ComputeLatestLinkBase(feedConfig);

            _logger.LogMessage(MessageImportance.High, "\nThe following aka.ms links for blobs will be created:");
            IEnumerable<AkaMSLink> linksToCreate = FilterAssetsToLink(assetsToPublish, feedConfig.FilenamesToExclude).Select(asset =>
            {

                // blob path.
                string actualTargetUrl = feedBaseUrl + asset;

                List<AkaMSLink> newLinks = new List<AkaMSLink>();
                foreach (string shortUrlPrefix in feedConfig.LatestLinkShortUrlPrefixes)
                {
                    newLinks.Add(GetAkaMSLinkForAsset(shortUrlPrefix, feedBaseUrl, asset, feedConfig.Flatten));
                }

                return newLinks;
            })
                .SelectMany(links => links)
                .ToList();

            await _linkManager.CreateOrUpdateLinksAsync(linksToCreate, _akaMSOwners, _akaMSCreatedBy, _akaMSGroupOwner, true);
        }

        public static IEnumerable<string> FilterAssetsToLink(HashSet<string> assetsToPublish, IEnumerable<string> filenamesToExclude) => assetsToPublish
                        .Where(asset => !filenamesToExclude.Contains(Path.GetFileName(asset)))
                        .Where(asset => !AkaMSDoNotCreateLinkPatterns.Any(p => p.IsMatch(asset)) &&
                                         AkaMSCreateLinkPatterns.Any(p => p.IsMatch(asset)));

        /// <summary>
        ///     Create the aka.ms link info
        /// </summary>
        /// <param name="shortUrlPrefix">aka.ms short url prefix</param>
        /// <param name="feedBaseUrl">Base feed url for the asset</param>
        /// <param name="asset">Asset</param>
        /// <param name="flatten">If we should only use the filename when creating the aka.ms link</param>
        /// <returns>The AkaMSLink object for the asset</returns>
        public AkaMSLink GetAkaMSLinkForAsset(string shortUrlPrefix, string feedBaseUrl, string asset, bool flatten)
        {
            // blob path.
            string actualTargetUrl = feedBaseUrl + asset;

            AkaMSLink newLink = new AkaMSLink
            {
                ShortUrl = GetLatestShortUrlForBlob(shortUrlPrefix, asset, flatten),
                TargetUrl = actualTargetUrl
            };
            _logger.LogMessage(MessageImportance.High, $"  {Path.GetFileName(asset)}");

            _logger.LogMessage(MessageImportance.High, $"  aka.ms/{newLink.ShortUrl} -> {newLink.TargetUrl}");

            return newLink;
        }

        /// <summary>
        ///     Get the short url for a blob.
        /// </summary>
        /// <param name="latestLinkShortUrlPrefix">aka.ms short url prefix</param>
        /// <param name="asset">Asset</param>
        /// <param name="flatten">If we should only use the filename when creating the aka.ms link</param>
        /// <returns>Short url prefix for the blob.</returns>
        public string GetLatestShortUrlForBlob(string latestLinkShortUrlPrefix, string asset, bool flatten)
        {
            string blobIdWithoutVersions = VersionIdentifier.RemoveVersions(asset);

            if (flatten)
            {
                blobIdWithoutVersions = Path.GetFileName(blobIdWithoutVersions);
            }

            return Path.Combine(latestLinkShortUrlPrefix, blobIdWithoutVersions).Replace("\\", "/");
        }
    }
}
