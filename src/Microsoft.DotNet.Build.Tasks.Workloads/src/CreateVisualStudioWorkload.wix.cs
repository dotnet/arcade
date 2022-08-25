// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Microsoft.DotNet.Build.Tasks.Workloads.Swix;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using static Microsoft.DotNet.Build.Tasks.Workloads.Msi.WorkloadManifestMsi;
using Parallel = System.Threading.Tasks.Parallel;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// An MSBuild task used to create workload artifacts including MSIs and SWIX projects for Visual Studio Installer.
    /// </summary>
    public class CreateVisualStudioWorkload : Task
    {
        /// <summary>
        /// A set of all supported MSI platforms.
        /// </summary>
        public static readonly string[] SupportedPlatforms = { "x86", "x64", "arm64" };

        /// <summary>
        /// The root intermediate output directory. This directory serves as a the base for generating
        /// installer sources and other projects used to create workload artifacts for Visual Studio.
        /// </summary>
        [Required]
        public string BaseIntermediateOutputPath
        {
            get;
            set;
        }

        /// <summary>
        /// The root output directory to use for compiled artifacts such as MSIs.
        /// </summary>
        [Required]
        public string BaseOutputPath
        {
            get;
            set;
        }

        /// <summary>
        /// A set of items that provide metadata associated with the Visual Studio components derived from
        /// workload manifests. 
        /// </summary>
        public ITaskItem[] ComponentResources
        {
            get;
            set;
        }

        /// <summary>
        /// A set of Internal Consistency Evaluators (ICEs) to suppress.
        /// </summary>
        public ITaskItem[] IceSuppressions
        {
            get;
            set;
        }

        /// <summary>
        /// The version to assign to workload manifest installers.
        /// </summary>
        public string ManifestMsiVersion
        {
            get;
            set;
        }

        /// <summary>
        /// A set of items containing all the MSIs that were generated. Additional metadata
        /// is provided for the projects that need to be built to produce NuGet packages for
        /// the MSI.
        /// </summary>
        [Output]
        public ITaskItem[] Msis
        {
            get;
            protected set;
        }

        /// <summary>
        /// The output path where MSIs will be placed.
        /// </summary>
        private string MsiOutputPath => Path.Combine(BaseOutputPath, "msi");

        /// <summary>
        /// The directory to use for locating workload pack packages.
        /// </summary>
        [Required]
        public string PackageSource
        {
            get;
            set;
        }

        /// <summary>
        /// Root directory where packages are extracted.
        /// </summary>
        private string PackageRootDirectory => Path.Combine(BaseIntermediateOutputPath, "pkg");

        /// <summary>
        /// A set of items used to shorten the names and identifiers of setup packages.
        /// </summary>
        public ITaskItem[] ShortNames
        {
            get;
            set;
        }

        /// <summary>
        /// A set of items containing .swixproj files that can be build to generate
        /// Visual Studio Installer components for workloads.
        /// </summary>
        [Output]
        public ITaskItem[] SwixProjects
        {
            get;
            protected set;
        }

        /// <summary>
        /// The directory containing the WiX toolset binaries.
        /// </summary>
        [Required]
        public string WixToolsetPath
        {
            get;
            set;
        }

        /// <summary>
        /// A set of packages containing workload manifests.
        /// </summary>
        [Required]
        public ITaskItem[] WorkloadManifestPackageFiles
        {
            get;
            set;
        }

        public bool CreateWorkloadPackGroups
        {
            get;
            set;
        }

        public bool UseWorkloadPackGroupsForVS
        {
            get;
            set;
        }

        /// <summary>
        /// If true, will skip creating MSIs for workload packs if they are part of a pack group
        /// </summary>
        public bool SkipRedundantMsiCreation
        {
            get;
            set;
        }

        public bool DisableParallelPackageGroupProcessing
        {
            get;
            set;
        }

        /// <summary>
        /// Allow VS workload generation to proceed if any nupkgs declared in the manifest are not found on disk.
        /// </summary>
        public bool AllowMissingPacks
        {
            get;
            set;
        } = false;

        public override bool Execute()
        {
            try
            {
                // TODO: trim out duplicate manifests.
                List<WorkloadManifestPackage> manifestPackages = new();
                List<WorkloadManifestMsi> manifestMsisToBuild = new();
                List<SwixComponent> swixComponents = new();
                Dictionary<string, BuildData> buildData = new();
                Dictionary<string, WorkloadPackGroupPackage> packGroupPackages = new();

                // First construct sets of everything that needs to be built. This includes
                // all the packages (manifests and workload packs) that need to be extracted along
                // with the different installer types. 
                foreach (ITaskItem workloadManifestPackageFile in WorkloadManifestPackageFiles)
                {
                    // 1. Process the manifest package and create a set of installers.
                    WorkloadManifestPackage manifestPackage = new(workloadManifestPackageFile, PackageRootDirectory, 
                        string.IsNullOrWhiteSpace(ManifestMsiVersion) ? null : new Version(ManifestMsiVersion), ShortNames, Log);
                    manifestPackages.Add(manifestPackage);

                    Dictionary<string, WorkloadManifestMsi> manifestMsisByPlatform = new();
                    foreach (string platform in SupportedPlatforms)
                    {
                        var manifestMsi = new WorkloadManifestMsi(manifestPackage, platform, BuildEngine, WixToolsetPath, BaseIntermediateOutputPath);
                        manifestMsisToBuild.Add(manifestMsi);
                        manifestMsisByPlatform[platform] = manifestMsi;
                    }

                    // 2. Process the manifest itself to determine the set of packs involved and create
                    //    installers for all the packs. Duplicate packs will be ignored, example, when
                    //    workloads in two manifests targeting different feature bands contain the
                    //    same pack dependencies. Building multiple copies of MSIs will cause
                    //    problems (ref counting, repair operations, etc.) and also increases the build time.
                    //
                    //    When building multiple manifests, it's possible for feature bands to have
                    //    different sets of packs. For example, the same manifest for different feature bands
                    //    can add additional platforms that requires generating additional SWIX projects, while
                    //    ensuring that the pack and MSI is only generated once.
                    WorkloadManifest manifest = manifestPackage.GetManifest();

                    List<WorkloadPackGroupJson> packGroupJsonList = new();

                    foreach (WorkloadDefinition workload in manifest.Workloads.Values)
                    {
                        if ((workload is WorkloadDefinition wd) && (wd.Platforms == null || wd.Platforms.Any(platform => platform.StartsWith("win"))) && (wd.Packs != null))
                        {
                            Dictionary<string, List<WorkloadPackPackage>> packsInWorkloadByPlatform = new();

                            string packGroupId = null;
                            WorkloadPackGroupJson packGroupJson = null;
                            if (CreateWorkloadPackGroups)
                            {
                                packGroupId = WorkloadPackGroupPackage.GetPackGroupID(workload.Id);
                                packGroupJson = new WorkloadPackGroupJson()
                                {
                                    GroupPackageId = packGroupId,
                                    GroupPackageVersion = manifestPackage.PackageVersion.ToString()
                                };
                                packGroupJsonList.Add(packGroupJson);
                            }
                            

                            foreach (WorkloadPackId packId in wd.Packs)
                            {
                                WorkloadPack pack = manifest.Packs[packId];

                                if (CreateWorkloadPackGroups)
                                {
                                    packGroupJson.Packs.Add(new WorkloadPackJson()
                                    {
                                        PackId = pack.Id,
                                        PackVersion = pack.Version
                                    });
                                }

                                foreach ((string sourcePackage, string[] platforms) in WorkloadPackPackage.GetSourcePackages(PackageSource, pack))
                                {
                                    if (!File.Exists(sourcePackage))
                                    {
                                        if (AllowMissingPacks)
                                        {
                                            Log.LogMessage($"Pack {sourcePackage} - {string.Join(",", platforms)} could not be found, it will be skipped.");
                                            continue;
                                        }
                                        else
                                        {
                                            throw new FileNotFoundException(message: "NuGet package not found", fileName: sourcePackage);
                                        }
                                    }

                                    // Create new build data and add the pack if we haven't seen it previously.
                                    if (!buildData.ContainsKey(sourcePackage))
                                    {
                                        buildData[sourcePackage] = new BuildData(WorkloadPackPackage.Create(pack, sourcePackage, platforms, PackageRootDirectory,
                                            ShortNames, Log));
                                    }

                                    foreach (string platform in platforms)
                                    {
                                        // If we haven't seen the platform, create a new entry, then add
                                        // the current feature band. This allows us to track platform specific packs
                                        // across multiple feature bands and manifests.
                                        if (!buildData[sourcePackage].FeatureBands.ContainsKey(platform))
                                        {
                                            buildData[sourcePackage].FeatureBands[platform] = new();
                                        }

                                        _ = buildData[sourcePackage].FeatureBands[platform].Add(manifestPackage.SdkFeatureBand);

                                        if (!packsInWorkloadByPlatform.ContainsKey(platform))
                                        {
                                            packsInWorkloadByPlatform[platform] = new();
                                        }
                                        packsInWorkloadByPlatform[platform].Add(buildData[sourcePackage].Package);
                                    }

                                    //  TODO: Find a better way to track this
                                    if (SkipRedundantMsiCreation)
                                    {
                                        buildData.Remove(sourcePackage);
                                    }
                                }
                            }                            

                            if (CreateWorkloadPackGroups)
                            {
                                //  TODO: Support passing in data to skip creating pack groups for certain packs (possibly EMSDK, because it's large)
                                foreach (var kvp in packsInWorkloadByPlatform)
                                {
                                    string platform = kvp.Key;

                                    //  The key is the paths to the packages included in the pack group, sorted in alphabetical order
                                    string uniquePackGroupKey = string.Join("\r\n", kvp.Value.Select(p => p.PackagePath).OrderBy(p => p));
                                    if (!packGroupPackages.TryGetValue(uniquePackGroupKey, out var groupPackage))
                                    {
                                        groupPackage = new WorkloadPackGroupPackage(workload.Id);
                                        groupPackage.Packs.AddRange(kvp.Value);
                                        packGroupPackages[uniquePackGroupKey] = groupPackage;
                                    }

                                    if (!groupPackage.ManifestsPerPlatform.ContainsKey(platform))
                                    {
                                        groupPackage.ManifestsPerPlatform[platform] = new();
                                    }
                                    groupPackage.ManifestsPerPlatform[platform].Add(manifestPackage);
                                }

                                foreach (var manifestMsi in manifestMsisByPlatform.Values)
                                {
                                    manifestMsi.WorkloadPackGroups.AddRange(packGroupJsonList);
                                }
                            }

                            // Finally, add a component for the workload in Visual Studio.
                            SwixComponent component = SwixComponent.Create(manifestPackage.SdkFeatureBand, workload, manifest, packGroupId,
                                ComponentResources, ShortNames);
                            swixComponents.Add(component);
                        }
                    }
                }

                List<ITaskItem> msiItems = new();
                List<ITaskItem> swixProjectItems = new();

                _ = Parallel.ForEach(buildData.Values, data =>
                {
                    // Extract the contents of the workload pack package.
                    Log.LogMessage(MessageImportance.Low, string.Format(Strings.BuildExtractingPackage, data.Package.PackagePath));
                    data.Package.Extract();

                    // Enumerate over the platforms and build each MSI once.
                    _ = Parallel.ForEach(data.FeatureBands.Keys, platform =>
                    {
                        WorkloadPackMsi msi = new(data.Package, platform, BuildEngine, WixToolsetPath, BaseIntermediateOutputPath);
                        ITaskItem msiOutputItem = msi.Build(MsiOutputPath, IceSuppressions);

                        // Generate a .csproj to package the MSI and its manifest for CLI installs.
                        MsiPayloadPackageProject csproj = new(msi.Metadata, msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath, msi.NuGetPackageFiles);
                        msiOutputItem.SetMetadata(Metadata.PackageProject, csproj.Create());

                        lock (msiItems)
                        {
                            msiItems.Add(msiOutputItem);
                        }

                        // We don't currently support arm64 SWIX authoring for VS. We'd need to pass a machineArch value
                        // depending on whether the feature band being processed supports arm64 so that we change the SWIX
                        // authoring to using machineArch instead of chip (which only works on older VS versions for x86/x64.
                        if (!string.Equals(msiOutputItem.GetMetadata(Metadata.Platform), "arm64"))
                        {
                            MsiSwixProject swixProject = new(msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath,
                                chip: msiOutputItem.GetMetadata(Metadata.Platform));
                            string swixProj = swixProject.Create();

                            foreach (ReleaseVersion sdkFeatureBand in data.FeatureBands[platform])
                            {
                                ITaskItem swixProjectItem = new TaskItem(swixProj);
                                swixProjectItem.SetMetadata(Metadata.SdkFeatureBand, $"{sdkFeatureBand}");

                                lock (swixProjectItems)
                                {
                                    swixProjectItems.Add(swixProjectItem);
                                }
                            }
                        }
                    });
                });

                //  Parallel processing of pack groups was causing file access errors for heat in an earlier version of this code
                //  So we support a flag to disable the parallelization if that starts happening again
                PossiblyParallelForEach(!DisableParallelPackageGroupProcessing, packGroupPackages.Values, packGroup =>
                {
                    foreach (var pack in packGroup.Packs)
                    {
                        pack.Extract();
                    }

                    foreach (var platform in packGroup.ManifestsPerPlatform.Keys)
                    {
                        WorkloadPackGroupMsi msi = new(packGroup, platform, BuildEngine, WixToolsetPath, BaseIntermediateOutputPath);
                        ITaskItem msiOutputItem = msi.Build(MsiOutputPath, IceSuppressions);

                        // Generate a .csproj to package the MSI and its manifest for CLI installs.
                        MsiPayloadPackageProject csproj = new(msi.Metadata, msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath, msi.NuGetPackageFiles);
                        msiOutputItem.SetMetadata(Metadata.PackageProject, csproj.Create());

                        lock (msiItems)
                        {
                            msiItems.Add(msiOutputItem);
                        }

                        if (UseWorkloadPackGroupsForVS)
                        {
                            // Skip generating arm64 SWIX authoring for now.
                            if (!string.Equals(msiOutputItem.GetMetadata(Metadata.Platform), "arm64"))
                            {
                                MsiSwixProject swixProject = new(msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath,
                                    chip: msiOutputItem.GetMetadata(Metadata.Platform));
                                string swixProj = swixProject.Create();

                                PossiblyParallelForEach(!DisableParallelPackageGroupProcessing, packGroup.ManifestsPerPlatform[platform], manifestPackage =>
                                {
                                    ITaskItem swixProjectItem = new TaskItem(swixProj);
                                    swixProjectItem.SetMetadata(Metadata.SdkFeatureBand, $"{manifestPackage.SdkFeatureBand}");

                                    lock (swixProjectItems)
                                    {
                                        swixProjectItems.Add(swixProjectItem);
                                    }
                                });
                            }
                        }
                    }
                });

                // Generate MSIs for the workload manifests along with
                // a .csproj to package the MSI and a SWIX project for
                // Visual Studio.
                _ = Parallel.ForEach(manifestMsisToBuild, msi =>
                {
                    ITaskItem msiOutputItem = msi.Build(MsiOutputPath, IceSuppressions);

                    // Skip generating arm64 SWIX authoring for now.
                    if (!string.Equals(msiOutputItem.GetMetadata(Metadata.Platform), "arm64"))
                    {
                        // Generate SWIX authoring for the MSI package.
                        MsiSwixProject swixProject = new(msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath,
                            chip: msiOutputItem.GetMetadata(Metadata.Platform));
                        ITaskItem swixProjectItem = new TaskItem(swixProject.Create());
                        swixProjectItem.SetMetadata(Metadata.SdkFeatureBand, $"{((WorkloadManifestPackage)msi.Package).SdkFeatureBand}");
                        swixProjectItem.SetMetadata(Metadata.PackageType, swixProject.PackageType);

                        lock (swixProjectItems)
                        {
                            swixProjectItems.Add(swixProjectItem);
                        }
                    }

                    // Generate a .csproj to package the MSI and its manifest for CLI installs.
                    MsiPayloadPackageProject csproj = new(msi.Metadata, msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath, msi.NuGetPackageFiles);
                    msiOutputItem.SetMetadata(Metadata.PackageProject, csproj.Create());

                    lock (msiItems)
                    {
                        msiItems.Add(msiOutputItem);
                    }
                });

                // Generate SWIX projects for the Visual Studio components. These are driven by the manifests, so
                // they need to be ordered based on feature bands to avoid pulling in unnecessary packs into the drop
                // artifacts.                
                _ = Parallel.ForEach(swixComponents, swixComponent =>
                {
                    ComponentSwixProject swixComponentProject = new(swixComponent, BaseIntermediateOutputPath, BaseOutputPath);
                    ITaskItem swixProjectItem = new TaskItem(swixComponentProject.Create());
                    swixProjectItem.SetMetadata(Metadata.SdkFeatureBand, $"{swixComponent.SdkFeatureBand}");
                    swixProjectItem.SetMetadata(Metadata.PackageType, swixComponentProject.PackageType);

                    lock (swixProjectItems)
                    {
                        swixProjectItems.Add(swixProjectItem);
                    }
                });

                Msis = msiItems.ToArray();
                SwixProjects = swixProjectItems.ToArray();
            }
            catch (Exception e)
            {
                Log.LogError(e.ToString());
            }

            return !Log.HasLoggedErrors;
        }

        static void PossiblyParallelForEach<T>(bool runInParallel, IEnumerable<T> source, Action<T> body)
        {
            if (runInParallel)
            {
                _ = Parallel.ForEach(source, body);
            }
            else
            {
                foreach (var item in source)
                {
                    body(item);
                }
            }
        }
    }
}
