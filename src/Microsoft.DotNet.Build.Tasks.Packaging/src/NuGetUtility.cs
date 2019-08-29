// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public static class NuGetUtility
    {
        internal static async Task<IEnumerable<Version>> GetAllVersionsForPackageIdAsync(string packageId, bool includePrerelease, bool includeUnlisted, Log log, CancellationToken cancellationToken)
        {
            List<Version> result = new List<Version>();
            ISettings settings = Settings.LoadDefaultSettings(Directory.GetCurrentDirectory());
            IEnumerable<PackageSource> enabledSources = GetEnabledSources(settings);
            var logger = new NuGetLogger(log);
            foreach (var packageSource in enabledSources)
            {
                using (var sourceCacheContext = new SourceCacheContext())
                {
                    var sourceRepository = new SourceRepository(packageSource, Repository.Provider.GetCoreV3());
                    var packageMetadataResource = await sourceRepository.GetResourceAsync<PackageMetadataResource>();
                    var searchMetadata = await packageMetadataResource.GetMetadataAsync(packageId, includePrerelease, includeUnlisted, sourceCacheContext, logger, cancellationToken);
                    foreach (IPackageSearchMetadata packageMetadata in searchMetadata)
                    {
                        if (!result.Contains(packageMetadata.Identity.Version.Version))
                            result.Add(VersionUtility.As3PartVersion(packageMetadata.Identity.Version.Version));
                    }
                }
            }
            // Given we are looking in different sources, we reorder all versions.
            return result.OrderBy(v => v);
        }

        private static IEnumerable<PackageSource> GetEnabledSources(ISettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var provider = new PackageSourceProvider(settings);
            return provider.LoadPackageSources().Where(e => e.IsEnabled == true).ToList();
        }

        public static Version GetLatestPatchStableVersionForRelease(this IEnumerable<Version> versions, int eraMajorVersion, int eraMinorVersion)
        {
            return versions.Where(v => VersionUtility.As2PartVersion(v) == new Version(eraMajorVersion, eraMinorVersion))
                           .OrderByDescending(v => v)
                           .FirstOrDefault();
        }

        internal class NuGetLogger : ILogger
        {
            private readonly Log _log;

            public NuGetLogger(Log log)
            {
                _log = log;
            }

            public void Log(LogLevel level, string data) => _log.LogMessage($"{level.ToString()} - {data}");

            public void Log(ILogMessage message) => _log.LogMessage(message.ToString());

            public Task LogAsync(LogLevel level, string data) => Task.Run(() => Log(level, data));

            public Task LogAsync(ILogMessage message) => Task.Run(() => Log(message));

            public void LogDebug(string data) => _log.LogMessage(data);

            public void LogError(string data) => _log.LogError(data);

            public void LogInformation(string data) => _log.LogMessage(data);

            public void LogInformationSummary(string data) => _log.LogMessage(data);

            public void LogMinimal(string data) => _log.LogMessage(data);

            public void LogVerbose(string data) => _log.LogMessage(data);

            public void LogWarning(string data) => _log.LogWarning(data);
        }
    }
}
