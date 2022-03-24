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
        public Version ManifestMsiVersion
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

        public override bool Execute()
        {
            try
            {
                // TODO: trim out duplicate manifests.
                List<WorkloadManifestPackage> manifestPackages = new();
                List<MsiBase> manifestMsisToBuild = new();
                List<SwixComponent> swixComponents = new();
                Dictionary<string, BuildData> buildData = new();

                // First construct sets of everything that needs to be built. This includes
                // all the packages (manifests and workload packs) that need to be extracted along
                // with the different installer types. 
                foreach (ITaskItem workloadManifestPackageFile in WorkloadManifestPackageFiles)
                {
                    // 1. Process the manifest package and create a set of installers.
                    WorkloadManifestPackage manifestPackage = new(workloadManifestPackageFile, PackageRootDirectory, ManifestMsiVersion, ShortNames, Log);
                    manifestPackages.Add(manifestPackage);

                    foreach (string platform in SupportedPlatforms)
                    {
                        manifestMsisToBuild.Add(new WorkloadManifestMsi(manifestPackage, platform, BuildEngine, WixToolsetPath, BaseIntermediateOutputPath));
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

                    foreach (WorkloadDefinition workload in manifest.Workloads.Values)
                    {
                        if ((workload is WorkloadDefinition wd) && (wd.Platforms == null || wd.Platforms.Any(platform => platform.StartsWith("win"))) && (wd.Packs != null))
                        {
                            foreach (WorkloadPackId packId in wd.Packs)
                            {
                                WorkloadPack pack = manifest.Packs[packId];

                                foreach ((string sourcePackage, string[] platforms) in WorkloadPackPackage.GetSourcePackages(PackageSource, pack))
                                {
                                    if (!File.Exists(sourcePackage))
                                    {
                                        throw new FileNotFoundException(message: null, fileName: sourcePackage);
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
                                    }

                                }
                            }

                            // Finally, add a component for the workload in Visual Studio.
                            SwixComponent component = SwixComponent.Create(manifestPackage.SdkFeatureBand, workload, manifest,
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
                        ITaskItem msiOutputItem = msi.Build(MsiOutputPath);

                        // Create the JSON manifest for CLI based installations.
                        string msiJsonPath = MsiProperties.Create(msiOutputItem.ItemSpec);

                        // Generate a .csproj to package the MSI and its manifest for CLI installs.
                        MsiPayloadPackageProject csproj = new(msi.Package, msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath, Path.GetFullPath(msiJsonPath));
                        msiOutputItem.SetMetadata(Metadata.PackageProject, csproj.Create());

                        lock (msiItems)
                        {
                            msiItems.Add(msiOutputItem);
                        }

                        MsiSwixProject swixProject = new(msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath);
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
                    });
                });

                // Generate MSIs for the workload manifests along with
                // a .csproj to package the MSI and a SWIX project for
                // Visual Studio.
                _ = Parallel.ForEach(manifestMsisToBuild, msi =>
                {
                    ITaskItem msiOutputItem = msi.Build(MsiOutputPath, IceSuppressions);

                    // Create the JSON manifest for CLI based installations.
                    string msiJsonPath = MsiProperties.Create(msiOutputItem.ItemSpec);

                    // Generate SWIX authoring for the MSI package.
                    MsiSwixProject swixProject = new(msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath);
                    ITaskItem swixProjectItem = new TaskItem(swixProject.Create());
                    swixProjectItem.SetMetadata(Metadata.SdkFeatureBand, $"{((WorkloadManifestPackage)msi.Package).SdkFeatureBand}");

                    lock (swixProjectItems)
                    {
                        swixProjectItems.Add(swixProjectItem);
                    }

                    // Generate a .csproj to package the MSI and its manifest for CLI installs.
                    MsiPayloadPackageProject csproj = new(msi.Package, msiOutputItem, BaseIntermediateOutputPath, BaseOutputPath, Path.GetFullPath(msiJsonPath));
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
                    ITaskItem swixProjItem = new TaskItem(swixComponentProject.Create());
                    swixProjItem.SetMetadata(Metadata.SdkFeatureBand, $"{swixComponent.SdkFeatureBand}");

                    lock (swixProjectItems)
                    {
                        swixProjectItems.Add(swixProjItem);
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
    }
}
