// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Versioning;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    /// <summary>
    /// MSBuild Task that validates if a package is harvesting
    /// the latest package version for a specific package era
    /// </summary>
    public class ValidateHarvestVersionIsLatestForEra : BuildTask
    {
        /// <summary>
        /// Path to the package report to be analyzed.
        /// </summary>
        [Required]
        public string PackageReportPath { get; set; }

        /// <summary>
        /// Version endpoints to be queried in order to get the latest stable patch
        /// version for a package era. The endpoints passed in should follow the following format:
        /// If the versions endpoint looks like: https://api.nuget.org/v3-flatcontainer/System.Runtime/index.json
        /// then we should pass in <![CDATA[<NugetPackageVersionsEndpoint Include="https://api.nuget.org/v3-flatcontainer/" />]]>
        /// The MSBuild task will use that base url to build the rest of it in order to get the list
        /// of versions for a given package.
        /// </summary>
        public ITaskItem[] NugetPackageVersionsEndpoints { get; set; }

        private const string NuGetDotOrgVersionEndpoint = @"https://api.nuget.org/v3-flatcontainer/";

        public override bool Execute()
        {
            PackageReport packageReport = GetPackageReportFromPath();
            bool isHarvestingAssetsFromPackage = TryGetHarvestVersionFromReport(packageReport, out string harvestVersion, out int harvestEraMajor, out int harvestEraMinor);

            if (isHarvestingAssetsFromPackage)
            {
                if (packageReport.Version.StartsWith($"{harvestEraMajor}.{harvestEraMinor}."))
                {
                    Log.LogError($"Validation Failed: {packageReport.Id} is harvesting package version {harvestVersion} which belongs to the current package era: {packageReport.Version}");
                    return false;
                }

                // If no package versions endpoints were provided, then default to use NuGet.org version endpoint.
                if (NugetPackageVersionsEndpoints == null || NugetPackageVersionsEndpoints.Length == 0)
                {
                    NugetPackageVersionsEndpoints = new TaskItem[]
                    {
                        new TaskItem(NuGetDotOrgVersionEndpoint)
                    };
                }

                string latestPatchVersion = GetLatestStableVersionForEraAsync(packageReport.Id, harvestEraMajor, harvestEraMinor).GetAwaiter().GetResult();
                if (latestPatchVersion.CompareTo(harvestVersion) != 0)
                {
                    Log.LogError($"Validation Failed: {packageReport.Id} is harvesting assets from package version {harvestVersion} which is not the latest for that package era. Latest package version from that era is {latestPatchVersion}.");
                }
                else
                {
                    Log.LogMessage(LogImportance.Normal, $"Validation Succeeded: {packageReport.Id} is harvesting assets from package version {harvestVersion} which is the latest for that package era.");
                }
            }
            else
            {
                Log.LogMessage(LogImportance.Normal, $"Validation Succeeded: {packageReport.Id} is not harvesting any assets.");
            }

            return !Log.HasLoggedErrors;
        }

        // Making this method protected virtual for tests.
        protected virtual PackageReport GetPackageReportFromPath()
        {
            return PackageReport.Load(PackageReportPath);
        }

        private bool TryGetHarvestVersionFromReport(PackageReport report, out string harvestVersion, out int harvestEraMajor, out int harvestEraMinor)
        {
            harvestVersion = string.Empty;
            harvestEraMajor = harvestEraMinor = 0;

            foreach (KeyValuePair<string, Target> packageTarget in report.Targets.NullAsEmpty())
            {
                foreach (PackageAsset compileAsset in packageTarget.Value.CompileAssets.NullAsEmpty())
                {
                    if (!string.IsNullOrEmpty(compileAsset.HarvestedFrom))
                    {
                        return GetHarvestVersionFromString(compileAsset.HarvestedFrom, report.Id, out harvestVersion, out harvestEraMajor, out harvestEraMinor);
                    }
                }

                foreach (PackageAsset runtimeAsset in packageTarget.Value.RuntimeAssets.NullAsEmpty())
                {
                    if (!string.IsNullOrEmpty(runtimeAsset.HarvestedFrom))
                    {
                        return GetHarvestVersionFromString(runtimeAsset.HarvestedFrom, report.Id, out harvestVersion, out harvestEraMajor, out harvestEraMinor);
                    }
                }

                foreach (PackageAsset nativeAsset in packageTarget.Value.NativeAssets.NullAsEmpty())
                {
                    if (!string.IsNullOrEmpty(nativeAsset.HarvestedFrom))
                    {
                        return GetHarvestVersionFromString(nativeAsset.HarvestedFrom, report.Id, out harvestVersion, out harvestEraMajor, out harvestEraMinor);
                    }
                }
            }

            return false;
        }

        private bool GetHarvestVersionFromString(string harvestedFrom, string packageId, out string harvestVersion, out int harvestEraMajor, out int harvestEraMinor)
        {
            harvestVersion = string.Empty;
            harvestEraMajor = harvestEraMinor = 0;
            string patternToSearchFor = $"{packageId}/";
            int startIndex = harvestedFrom.IndexOf(patternToSearchFor);
            if (startIndex != -1)
            {
                startIndex += patternToSearchFor.Length;
                int endIndex = harvestedFrom.IndexOf("/", startIndex);
                if (endIndex != -1)
                {
                    harvestVersion = harvestedFrom.Substring(startIndex, endIndex - startIndex);
                    NuGetVersion harvestPackageVersion = new NuGetVersion(harvestVersion);
                    harvestEraMajor = harvestPackageVersion.Major;
                    harvestEraMinor = harvestPackageVersion.Minor;
                    return true;
                }
                else
                {
                    Log.LogError($"Failed to parse package version from string: {harvestedFrom}");
                    return false;
                }
            }
            else
            {
                Log.LogError($"Failed to parse package version from string: {harvestedFrom}");
                return false;
            }
        }

        // Making this method protected virtual for tests.
        protected virtual async Task<string> GetLatestStableVersionForEraAsync(string packageId, int eraMajorVersion, int eraMinorVersion)
        {
            string latestPatchVersion = string.Empty;
            foreach (var versionEndpoint in NugetPackageVersionsEndpoints)
            {
                PackageVersions packageVersions = await GetAllVersionsFromPackageId(packageId, versionEndpoint, Log);
                string latestPatchFromFeed = packageVersions.GetLatestPatchStableVersionForEra(eraMajorVersion, eraMinorVersion, Log);
                // CompareTo method will return 1 if latestPatchVersion is empty so no need to add check.
                if (latestPatchFromFeed.CompareTo(latestPatchVersion) > 0)
                {
                    latestPatchVersion = latestPatchFromFeed;
                }
            }
            return latestPatchVersion;
        }

        internal static async Task<PackageVersions> GetAllVersionsFromPackageId(string packageId, ITaskItem versionEndpoint, Log log)
        {
            string allPackageVersionsUrl = string.Concat(versionEndpoint.ItemSpec, packageId, "/index.json");
            string versionsJson = string.Empty;

            using (HttpClient httpClient = new HttpClient())
            {
                using (HttpResponseMessage httpResponseMessage = await httpClient.GetAsync(allPackageVersionsUrl))
                {
                    if (httpResponseMessage.StatusCode != HttpStatusCode.OK)
                    {
                        log.LogError($"Unable to reach the package versions url at {allPackageVersionsUrl}. Recieved status code {httpResponseMessage.StatusCode}.");
                        return null;
                    }
                    versionsJson = await httpResponseMessage.Content.ReadAsStringAsync();
                }
            }

            return JsonSerializer.Deserialize<PackageVersions>(versionsJson);
        }

        internal class PackageVersions
        {
            [JsonPropertyName("versions")]
            public List<string> Versions { get; set; }

            public string GetLatestPatchStableVersionForEra(int major, int minor, Log buildlog)
            {
                string result = string.Empty;

                foreach (var version in Versions)
                {
                    NuGetVersion nugetVersion = new NuGetVersion(version);
                    if (!nugetVersion.IsPrerelease && nugetVersion.Major == major && nugetVersion.Minor == minor)
                    {
                        result = version;
                    }
                }

                if (string.IsNullOrEmpty(result))
                {
                    buildlog.LogError($"There are no stable versions that match {major}.{minor}.* package version.");
                }

                return result;
            }
        }
    }
}
