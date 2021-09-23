// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        /// The path where the workload-pack packages referenced by the manifest files are located.
        /// </summary>
        public string PackagesPath
        {
            get;
            set;
        }

        /// <summary>
        /// Generate msis in parallel.
        /// </summary>
        public bool RunInParallel
        {
            get;
            set;
        } = true;

        /// <summary>
        /// Gets the set of missing workload packs.
        /// </summary>
        [Output]
        public ITaskItem[] MissingPacks
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                List<ITaskItem> msis = new();
                List<ITaskItem> missingPacks = new();

                if (string.IsNullOrWhiteSpace(PackagesPath))
                {
                    Log.LogError($"{nameof(PackagesPath)} parameter cannot be null or empty.");
                    return false;
                }

                // Each pack maps to multiple packs and different MSI packages. We consider a pack
                // to be missing when none of its dependent MSIs were found/generated.
                IEnumerable<WorkloadPack> workloadPacks = GetWorkloadPacks();
                List<string> missingPackIds = new(workloadPacks.Select(p => $"{p.Id}"));

                List<(string sourcePackage, string swixPackageId, string outputPath, WorkloadPackKind kind, string[] platforms)> packsToGenerate = new();

                foreach (WorkloadPack pack in workloadPacks)
                {
                    Log.LogMessage($"Processing workload pack: {pack.Id}, Version: {pack.Version}");

                    foreach ((string sourcePackage, string[] platforms) in GetSourcePackages(pack))
                    {
                        if (!File.Exists(sourcePackage))
                        {
                            Log?.LogMessage(MessageImportance.High, $"Workload pack package does not exist: {sourcePackage}");

                            missingPacks.Add(new TaskItem($"{pack.Id}", new Dictionary<string, string>
                            {
                                { Metadata.SourcePackage, sourcePackage },
                                { Metadata.Platform, string.Join(",", platforms) },
                                { Metadata.ShortName, $"{pack.Id.ToString().Replace(ShortNames)}" }
                            }));

                            continue;
                        }

                        // Swix package is always versioned to support upgrading SxS installs. The pack alias will be
                        // used for individual MSIs
                        string swixPackageId = $"{pack.Id.ToString().Replace(ShortNames)}.{pack.Version}";

                        // Always select the pack ID for the VS MSI package, even when aliased.
                        if (RunInParallel)
                        {
                            packsToGenerate.Add(new(sourcePackage, swixPackageId, OutputPath, pack.Kind, platforms));
                        }
                        else
                        {
                            msis.AddRange(Generate(sourcePackage, swixPackageId, OutputPath, pack.Kind, platforms));
                        }
                    }
                }

                if (RunInParallel)
                {
                    System.Threading.Tasks.Parallel.ForEach(packsToGenerate, p =>
                    {
                        msis.AddRange(Generate(p.sourcePackage, p.swixPackageId, p.outputPath, p.kind, p.platforms));
                    });
                }

                Msis = msis.ToArray();
                MissingPacks = missingPacks.ToArray();
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
                w => WorkloadManifestReader.ReadWorkloadManifest(Path.GetFileNameWithoutExtension(w.ItemSpec), File.OpenRead(w.ItemSpec)));

            // We want all workloads in all manifests iff the workload has no platform or at least one
            // platform includes Windows
            var workloads = manifests.SelectMany(m => m.Workloads).
                Select(w => w.Value).
                Where(wd => (wd.Platforms == null) || wd.Platforms.Any(p => p.StartsWith("win")));

            var packIds = workloads.Where(w => w.Packs != null).SelectMany(w => w.Packs).Distinct();

            return manifests.SelectMany(m => m.Packs.Values).
                Where(p => packIds.Contains(p.Id)).
                Distinct();
        }

        /// <summary>
        /// Gets the packages associated with a specific workload pack.
        /// </summary>
        /// <param name="pack"></param>
        /// <returns>An enumerable of tuples. Each tuple contains the full path of the NuGet package and the target platforms.</returns>
        internal IEnumerable<(string, string[])> GetSourcePackages(WorkloadPack pack)
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
                            continue;
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
