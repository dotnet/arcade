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
    /// MSBuild task for generating Visual Studio component projects representing
    /// the workload definitions.
    /// </summary>
    public class GenerateVisualStudioWorkload : GenerateTaskBase
    {
        /// <summary>
        /// An item group used to provide a customized title, description, and category for a specific workload ID in Visual Studio.
        /// Workloads only define a description. Visual Studio defines a separate title (checkbox text) and description (checkbox tooltip).
        /// </summary>
        public ITaskItem[] ComponentResources
        {
            get;
            set;
        }

        /// <summary>
        /// The version of the component in the Visual Studio manifest. If no version is specified,
        /// the manifest version is used.
        /// </summary>
        public ITaskItem[] ComponentVersions
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets whether MSIs for workload packs will be generated. When set to <see langword="false" />, only
        /// Visual Studio component authoring files are generated.
        /// </summary>
        public bool GenerateMsis
        {
            get;
            set;
        } = true;

        /// <summary>
        /// Set of missing workload pack packages.
        /// </summary>
        [Output]
        public ITaskItem[] MissingPacks
        {
            get;
            set;
        } = Array.Empty<ITaskItem>();

        public string OutputPath
        {
            get;
            set;
        }

        /// <summary>
        /// The path where the workload-pack packages referenced by the workload manifests are located.
        /// </summary>
        public string PackagesPath
        {
            get;
            set;
        }

        /// <summary>
        /// An item group containing information to shorten the names of packages.
        /// </summary>
        public ITaskItem[] ShortNames
        {
            get;
            set;
        }

        /// <summary>
        /// The workload manifest files to use for generating the Visual Studio components.
        /// </summary>
        public ITaskItem[] WorkloadManifests
        {
            get;
            set;
        }

        /// <summary>
        /// A set of packages containing workload manifests.
        /// </summary>
        public ITaskItem[] WorkloadPackages
        {
            get;
            set;
        }

        /// <summary>
        /// Semicolon sepearate list of ICEs to suppress.
        /// </summary>
        public string SuppressIces
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
        /// The paths of the generated .swixproj files.
        /// </summary>
        [Output]
        public ITaskItem[] SwixProjects
        {
            get;
            set;
        }

        [Output]
        public ITaskItem[] Msis
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                if (WorkloadManifests != null)
                {
                    SwixProjects = GenerateSwixProjects(WorkloadManifests);
                }
                else if (WorkloadPackages != null)
                {
                    SwixProjects = GenerateSwixProjects(GetManifestsFromManifestPackages(WorkloadPackages));
                }
                else
                {
                    Log.LogError($"Either {nameof(WorkloadPackages)} or {nameof(WorkloadManifests)} item must be non-empty");
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        ITaskItem[] GenerateSwixProjects(ITaskItem[] workloadManifests)
        {
            List<ITaskItem> swixProjects = new();

            // Generate the MSIs first to see if we have missing packs so we can remove
            // those dependencies from the components.
            if (GenerateMsis)
            {
                Log?.LogMessage(MessageImportance.Low, "Generating MSIs...");
                // Generate MSIs for workload packs and add their .swixproj files 
                swixProjects.AddRange(GenerateMsisFromManifests(workloadManifests));
            }

            foreach (ITaskItem workloadManifest in workloadManifests)
            {
                swixProjects.AddRange(ProcessWorkloadManifestFile(workloadManifest.GetMetadata("FullPath")));
            }

            return swixProjects.ToArray();
        }

        internal IEnumerable<ITaskItem> GenerateMsisFromManifests(ITaskItem[] workloadManifests)
        {
            GenerateWorkloadMsis msiTask = new()
            {
                BuildEngine = this.BuildEngine,
                GenerateSwixAuthoring = true,
                IntermediateBaseOutputPath = this.IntermediateBaseOutputPath,
                OutputPath = this.OutputPath,
                PackagesPath = this.PackagesPath,
                RunInParallel = this.RunInParallel,
                ShortNames = this.ShortNames,
                SuppressIces = this.SuppressIces,
                WixToolsetPath = this.WixToolsetPath,
                WorkloadManifests = workloadManifests
            };

            if (!msiTask.Execute())
            {
                Log?.LogError($"Failed to generate MSIs for workload packs.");
                return Enumerable.Empty<ITaskItem>();
            }
            else
            {
                if (msiTask.MissingPacks != null)
                {
                    MissingPacks = msiTask.MissingPacks;

                    foreach (ITaskItem item in MissingPacks)
                    {
                        Log?.LogMessage(MessageImportance.High, $"Unable to locate '{item.GetMetadata(Metadata.SourcePackage)}'. Short name: {item.GetMetadata(Metadata.ShortName)}, Platform: {item.GetMetadata(Metadata.Platform)}, Workload Pack: ({item.ItemSpec}).");
                    }
                }

                Msis = msiTask.Msis;

                // The Msis output parameter also contains the .swixproj files, but for VS, we want all the project files for
                // packages and components.
                return msiTask.Msis.Where(m => !string.IsNullOrWhiteSpace(m.GetMetadata(Metadata.SwixProject))).
                    Select(m => new TaskItem(m.GetMetadata(Metadata.SwixProject)));
            }
        }

        internal IEnumerable<ITaskItem> ProcessWorkloadManifestFile(string workloadManifestJsonPath)
        {
            WorkloadManifest manifest = WorkloadManifestReader.ReadWorkloadManifest(Path.GetFileNameWithoutExtension(workloadManifestJsonPath), File.OpenRead(workloadManifestJsonPath));

            List<TaskItem> swixProjects = new();

            foreach (WorkloadDefinition workloadDefinition in manifest.Workloads.Values)
            {
                if ((workloadDefinition.Platforms?.Count > 0) && (!workloadDefinition.Platforms.Any(p => p.StartsWith("win"))))
                {
                    Log?.LogMessage(MessageImportance.High, $"{workloadDefinition.Id} platforms does not support Windows and will be skipped ({string.Join(", ", workloadDefinition.Platforms)}).");
                    continue;
                }

                // Each workload maps to a Visual Studio component.
                VisualStudioComponent component = VisualStudioComponent.Create(Log, manifest, workloadDefinition,
                    ComponentVersions, ShortNames, ComponentResources, MissingPacks);

                // If there are no dependencies, regardless of whether we are generating MSIs, we'll report an
                // error as we'd produce invalid SWIX.
                if (!component.HasDependencies)
                {
                    Log?.LogError($"Visual Studio components '{component.Name}' must have at least one dependency.");
                }

                string vsPayloadRelativePath = $"{component.Name},version={component.Version}\\_package.json";
                CheckRelativePayloadPath(vsPayloadRelativePath);

                swixProjects.Add(component.Generate(Path.Combine(SourceDirectory, $"{workloadDefinition.Id}.{manifest.Version}.0")));
            }

            return swixProjects;
        }

        /// <summary>
        /// Extracts the workload manifest from the manifest package and generate a SWIX project for a Visual Studio component
        /// matching the manifests dependencies.  
        /// </summary>
        /// <param name="workloadManifestPackage">The path of the workload package containing the manifest.</param>
        /// <returns>A set of items containing the generated SWIX projects.</returns>
        internal IEnumerable<ITaskItem> ProcessWorkloadManifestPackage(string workloadManifestPackage)
        {
            NugetPackage workloadPackage = new(workloadManifestPackage, Log);
            string packageContentPath = Path.Combine(PackageDirectory, $"{workloadPackage.Identity}");
            workloadPackage.Extract(packageContentPath, Enumerable.Empty<string>());

            return ProcessWorkloadManifestFile(GetWorkloadManifestJsonPath(packageContentPath));
        }

        internal ITaskItem[] GetManifestsFromManifestPackages(ITaskItem[] workloadPackages)
        {
            List<TaskItem> manifests = new();

            foreach (ITaskItem item in workloadPackages)
            {
                NugetPackage workloadPackage = new(item.GetMetadata("FullPath"), Log);
                string packageContentPath = Path.Combine(PackageDirectory, $"{workloadPackage.Identity}");
                workloadPackage.Extract(packageContentPath, Enumerable.Empty<string>());
                string workloadManifestJsonPath = GetWorkloadManifestJsonPath(packageContentPath);
                Log?.LogMessage(MessageImportance.Low, $"Adding manifest: {workloadManifestJsonPath}");

                manifests.Add(new TaskItem(workloadManifestJsonPath));
            }

            return manifests.ToArray();
        }

        internal string GetWorkloadManifestJsonPath(string packageContentPath)
        {
            string dataDirectory = Path.Combine(packageContentPath, "data");

            // Check the data directory first, otherwise fall back to the older format where manifests were in the root of the package.
            string workloadManifestJsonPath = Directory.Exists(dataDirectory) ?
                Directory.GetFiles(dataDirectory, "WorkloadManifest.json").FirstOrDefault() :
                Directory.GetFiles(packageContentPath, "WorkloadManifest.json").FirstOrDefault();

            if (string.IsNullOrWhiteSpace(workloadManifestJsonPath))
            {
                throw new FileNotFoundException($"Unable to locate WorkloadManifest.json under '{packageContentPath}'.");
            }

            return workloadManifestJsonPath;
        }
    }
}
