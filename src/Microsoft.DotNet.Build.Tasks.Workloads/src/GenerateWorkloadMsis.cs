// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// MSBuild task to generate a set of MSIs from a set of workload manifests.
    /// </summary>
    public class GenerateWorkloadMsis : GenerateMsiBase
    {
        /// <summary>
        /// The workload manifests files to process.
        /// </summary>
        [Required]
        public ITaskItem[] WorkloadManifests
        {
            get;
            set;
        }

        /// <summary>
        /// The path where the packages referenced by the manifest files are located.
        /// </summary>
        public string PackagesPath
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                List<ITaskItem> msis = new();

                if (string.IsNullOrWhiteSpace(PackagesPath))
                {
                    Log.LogError($"{nameof(PackagesPath)} parameter cannot be null or empty.");
                    return false;
                }

                foreach (WorkloadPack pack in GetWorkloadPacks())
                {
                    Log.LogMessage($"Processing workload pack: {pack.Id}, Version: {pack.Version}");

                    foreach ((string sourcePackage, string[] platforms) in GetSourcePackages(pack))
                    {
                        // Always select the pack ID for the VS MSI package.
                        msis.AddRange(Generate(sourcePackage, $"{pack.Id}", OutputPath, GetInstallDir(pack.Kind), platforms));
                    }
                }

                Msis = msis.ToArray();
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        private IEnumerable<WorkloadPack> GetWorkloadPacks()
        {
            // We need to track duplicate packs so we only generate MSIs once. We'll key off the pack ID and version.
            IEnumerable<WorkloadManifest> manifests = WorkloadManifests.Select(
                w => WorkloadManifestReader.ReadWorkloadManifest(File.OpenRead(w.ItemSpec)));

            return manifests.SelectMany(m => m.Packs.Values).GroupBy(x => new { x.Id, x.Version }).
                Select(g => g.First());
        }

        private IEnumerable<(string, string[])> GetSourcePackages(WorkloadPack pack)
        {
            if (pack.IsAlias)
            {
                foreach (string rid in pack.AliasTo.Keys)
                {
                    string sourcePackage = Path.Combine(PackagesPath, $"{pack.AliasTo[rid]}.{pack.Version}.nupkg");

                    switch (rid)
                    {
                        case "win7":
                        case "win10":
                        case "win":
                        case "any":
                            yield return (sourcePackage, SupportedPlatforms);
                            break;
                        case "win-x64":
                            yield return (sourcePackage, new[] { "x64" });
                            break;
                        case "win-x86":
                            yield return (sourcePackage, new[] { "x86" });
                            break;
                        case "win-arm64":
                            yield return (sourcePackage, new[] { "arm64" });
                            break;
                        default:
                            Log?.LogMessage($"Skipping alias ({rid}).");
                            break;
                    }
                }
            }
            else
            {
                // For non-RID specific packs we'll produce MSIs for each supported platform.
                yield return (Path.Combine(PackagesPath, $"{pack.Id}.{pack.Version}.nupkg"), SupportedPlatforms);
            }
        }
    }
}
