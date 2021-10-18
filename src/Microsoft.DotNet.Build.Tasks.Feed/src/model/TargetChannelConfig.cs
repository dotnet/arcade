// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;

namespace Microsoft.DotNet.Build.Tasks.Feed.Model
{
    /**
     * The goal of this class is to configure a channel that a build can be promoted to,
     * most of the information here is just an extension of the info already present in
     * BAR. However, a big difference in relation to BAR Channel is that this class has
     * the `PublishingInfraVersion` that describes which version of the publishing infra
     * structure the channel configuration is applicable.
     */
    public struct TargetChannelConfig
    {
        /// <summary>
        /// Which version of the publishing infra can use this configuration.
        /// </summary>
        public PublishingInfraVersion PublishingInfraVersion { get; }

        /// <summary>
        /// BAR Channel ID
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// The name that should be used for creating Aka.ms links for this channel.
        /// </summary>
        public string AkaMSChannelName { get; }

        public ImmutableList<TargetFeedSpecification> TargetFeeds { get; }

        /// <summary>
        /// Should publish to Msdl
        /// </summary>
        public SymbolTargetType SymbolTargetType { get; }

        public bool IsInternal { get; }

        public ImmutableList<string> FilenamesToExclude { get; }

        public bool Flatten { get; }

        public TargetChannelConfig(
            int id,
            bool isInternal,
            PublishingInfraVersion publishingInfraVersion,
            string akaMSChannelName,
            IEnumerable<TargetFeedSpecification> targetFeeds,
            SymbolTargetType symbolTargetType,
            List<string> filenamesToExclude = null,
            bool flatten = true)
        {
            Id = id;
            IsInternal = isInternal;
            PublishingInfraVersion = publishingInfraVersion;
            AkaMSChannelName = akaMSChannelName;
            TargetFeeds = targetFeeds.ToImmutableList();
            SymbolTargetType = symbolTargetType;
            FilenamesToExclude = filenamesToExclude?.ToImmutableList() ?? ImmutableList<string>.Empty;
            Flatten = flatten;
        }

        public override string ToString()
        {
            return
                $"\n Channel ID: '{Id}' " +
                $"\n Infra-version: '{PublishingInfraVersion}' " +
                $"\n AkaMSChannelName: '{AkaMSChannelName}' " +
                "\n Target Feeds:" +
                $"\n  {string.Join("\n  ", TargetFeeds.Select(f => $"{string.Join(", ", f.ContentTypes)} -> {f.FeedUrl}"))}" +
                $"\n SymbolTargetType: '{SymbolTargetType}' " +
                $"\n IsInternal: '{IsInternal}'" +
                $"\n FilenamesToExclude: \n\t{string.Join("\n\t", FilenamesToExclude)}" +
                $"\n Flatten: '{Flatten}'";
        }

        public override bool Equals(object other)
        {
            if (other is TargetChannelConfig config &&
                PublishingInfraVersion == config.PublishingInfraVersion &&
                Id == config.Id &&
                string.Equals(AkaMSChannelName, config.AkaMSChannelName, StringComparison.OrdinalIgnoreCase) &&
                TargetFeeds.Count == config.TargetFeeds.Count &&
                TargetFeeds.Zip(config.TargetFeeds, (l, r) => l.Equals(r)).All(b => b) &&
                IsInternal == config.IsInternal &&
                Flatten == config.Flatten)
            {
                if (FilenamesToExclude is null)
                    return config.FilenamesToExclude is null;
                
                if (config.FilenamesToExclude is null)
                    return false;
                
                return FilenamesToExclude.SequenceEqual(config.FilenamesToExclude);
            }
            
            return false;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(PublishingInfraVersion);
            hash.Add(Id);
            hash.Add(IsInternal);
            hash.Add(AkaMSChannelName);
            foreach (var feedSpec in TargetFeeds)
            {
                hash.Add(feedSpec);
            }
            hash.Add(SymbolTargetType);
            hash.Add(Flatten);
            foreach (string fileName in FilenamesToExclude)
            {
                hash.Add(fileName);
            }

            return hash.ToHashCode();
        }
    }

    public struct TargetFeedSpecification
    {
        public ImmutableList<TargetFeedContentType> ContentTypes { get; }
        public string FeedUrl { get; }
        public AssetSelection Assets { get; }

        public static implicit operator TargetFeedSpecification((TargetFeedContentType[] types, string feed) tuple)
        {
            return new TargetFeedSpecification(tuple.types, tuple.feed, AssetSelection.All);
        }

        public static implicit operator TargetFeedSpecification((TargetFeedContentType[] types, string feed, AssetSelection assets) tuple)
        {
            return new TargetFeedSpecification(tuple.types, tuple.feed, tuple.assets);
        }

        public static implicit operator TargetFeedSpecification((TargetFeedContentType type, string feed) tuple)
        {
            return new TargetFeedSpecification(ImmutableList.Create(tuple.type), tuple.feed, AssetSelection.All);
        }

        public static implicit operator TargetFeedSpecification((TargetFeedContentType type, string feed, AssetSelection assets) tuple)
        {
            return new TargetFeedSpecification(ImmutableList.Create(tuple.type), tuple.feed, tuple.assets);
        }

        public TargetFeedSpecification(IEnumerable<TargetFeedContentType> contentTypes, string feedUrl, AssetSelection assets)
        {
            ContentTypes = contentTypes.ToImmutableList();
            FeedUrl = feedUrl;
            Assets = assets;
        }

        public override bool Equals(object obj)
        {
            return obj is TargetFeedSpecification other && Equals(other);
        }

        public bool Equals(TargetFeedSpecification other) => ContentTypes.Count == other.ContentTypes.Count &&
                                                             ContentTypes.Zip(other.ContentTypes, (l, r) => l.Equals(r)).All(b => b) &&
                                                             FeedUrl == other.FeedUrl;

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var t in ContentTypes)
            {
                hash.Add(t);
            }
            hash.Add(FeedUrl);
            return hash.ToHashCode();
        }
    }
}
