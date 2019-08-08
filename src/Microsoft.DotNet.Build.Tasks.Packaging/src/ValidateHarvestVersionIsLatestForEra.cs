// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;

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
        /// version for a package era.
        /// </summary>
        public ITaskItem[] NugetPackageVersionsEndpoints { get; set; }

        private const string NuGetDotOrgVersionEndpoint = @"https://api.nuget.org/v3-flatcontainer/";

        public override bool Execute()
        {
            PackageReport packageReport = GetPackageReportFromPath();
            bool isHarvestingAssetsFromPackage = TryGetHarvestVersionFromReport(packageReport, out string harvestVersion, out int harvestEraMajor, out int harvestEraMinor);

            if (isHarvestingAssetsFromPackage)
            {
                // If no package versions endpoints were provided, then default to use NuGet.org version endpoint.
                if (NugetPackageVersionsEndpoints == null || NugetPackageVersionsEndpoints.Length == 0)
                {
                    NugetPackageVersionsEndpoints = new TaskItem[]
                    {
                        new TaskItem(NuGetDotOrgVersionEndpoint)
                    };
                }

                string latestPatchVersion = GetLatestStableVersionForEra(packageReport.Id, harvestEraMajor, harvestEraMinor);
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
            Regex regex = new Regex($"{report.Id}/(\\d*).(\\d*).(\\d*)/");

            foreach (KeyValuePair<string, Target> packageTarget in report.Targets.NullAsEmpty())
            {
                foreach (PackageAsset compileAsset in packageTarget.Value.CompileAssets.NullAsEmpty())
                {
                    if (!string.IsNullOrEmpty(compileAsset.HarvestedFrom))
                    {
                        return MatchesHarvestVersionPattern(regex, compileAsset.HarvestedFrom, out harvestVersion, out harvestEraMajor, out harvestEraMinor);
                    }
                }

                foreach (PackageAsset runtimeAsset in packageTarget.Value.RuntimeAssets.NullAsEmpty())
                {
                    if (!string.IsNullOrEmpty(runtimeAsset.HarvestedFrom))
                    {
                        return MatchesHarvestVersionPattern(regex, runtimeAsset.HarvestedFrom, out harvestVersion, out harvestEraMajor, out harvestEraMinor);
                    }
                }

                foreach (PackageAsset nativeAsset in packageTarget.Value.NativeAssets.NullAsEmpty())
                {
                    if (!string.IsNullOrEmpty(nativeAsset.HarvestedFrom))
                    {
                        return MatchesHarvestVersionPattern(regex, nativeAsset.HarvestedFrom, out harvestVersion, out harvestEraMajor, out harvestEraMinor);
                    }
                }
            }

            return false;
        }

        private bool MatchesHarvestVersionPattern(Regex regex, string harvestedFrom, out string harvestVersion, out int harvestEraMajor, out int harvestEraMinor)
        {
            var match = regex.Match(harvestedFrom);
            if (match.Success)
            {
                harvestEraMajor = int.Parse(match.Groups[1].Value);
                harvestEraMinor = int.Parse(match.Groups[2].Value);
                harvestVersion = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
                return true;
            }
            else
            {
                harvestVersion = string.Empty;
                harvestEraMajor = harvestEraMinor = 0;
                Log.LogError($"Failed to get harvest version from string {harvestedFrom}");
                return false;
            }
        }

        // Making this method protected virtual for tests.
        protected virtual string GetLatestStableVersionForEra(string packageId, int eraMajorVersion, int eraMinorVersion)
        {
            string latestPatchVersion = string.Empty;
            foreach (var versionEndpoint in NugetPackageVersionsEndpoints)
            {

                string allPackageVersionsUrl = string.Concat(versionEndpoint.ItemSpec, packageId, "/index.json");
                string versionsJson = string.Empty;

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(allPackageVersionsUrl);
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        Log.LogError($"Unable to reach the package versions url at {allPackageVersionsUrl}. Recieved status code {response.StatusCode}.");
                        return null;
                    }
                    using (var stream = response.GetResponseStream())
                    using (var reader = new StreamReader(stream))
                    {
                        versionsJson = reader.ReadToEnd();
                    }
                }

                PackageVersions packageVersions = JsonConvert.DeserializeObject<PackageVersions>(versionsJson);
                string latestPatchFromFeed = packageVersions.GetLatestPatchStableVersionForEra(eraMajorVersion, eraMinorVersion, Log);
                // CompareTo method will return 1 if latestPatchVersion is empty so no need to add check.
                if (latestPatchFromFeed.CompareTo(latestPatchVersion) > 0)
                {
                    latestPatchVersion = latestPatchFromFeed;
                }
            }
            return latestPatchVersion;
        }

        private class PackageVersions
        {
            [JsonProperty(PropertyName = "versions")]
            public List<string> Versions { get; set; }

            public string GetLatestPatchStableVersionForEra(int major, int minor, Log buildlog)
            {
                string result = string.Empty;

                foreach (var version in Versions)
                {
                    if (IsStableVersion(version) && version.StartsWith($"{major}.{minor}"))
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

            private bool IsStableVersion(string version)
            {
                return version.IndexOf("-") == -1;
            }
        }
    }
}
