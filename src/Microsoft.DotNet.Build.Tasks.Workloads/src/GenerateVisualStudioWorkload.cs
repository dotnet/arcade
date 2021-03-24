// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Build.Tasks.Workloads.src
{
    /// <summary>
    /// MSBuild task for generating Visual Studio component projects representing
    /// the workload definitions.
    /// </summary>
    public class GenerateVisualStudioWorkload : GenerateTaskBase
    {
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
        /// 
        /// </summary>
        [Output]
        public ITaskItem[] SwixProjects
        {
            get;
            set;
        }

        public override bool Execute()
        {
            try
            {
                List<ITaskItem> swixProjects = new();

                foreach (ITaskItem workloadPackage in WorkloadPackages)
                {
                    swixProjects.AddRange(ProcessWorkloadManifest(workloadPackage.GetMetadata("FullPath")));
                }

                SwixProjects = swixProjects.ToArray();
            }
            catch (Exception e)
            {
                Log.LogMessage(MessageImportance.Low, e.StackTrace);
                Log.LogErrorFromException(e);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Extracts the workload manifest from the manifest package and generate a SWIX project for a Visual Studio component
        /// matching the manifests dependencies.  
        /// </summary>
        /// <param name="workloadManifestPackage">The path of the workload package containing the manifest.</param>
        /// <returns>A set of items containing the generated SWIX projects.</returns>
        internal IEnumerable<ITaskItem> ProcessWorkloadManifest(string workloadManifestPackage)
        {
            NugetPackage workloadPackage = new NugetPackage(workloadManifestPackage, Log);

            string packageContentPath = Path.Combine(PackageDirectory, $"{workloadPackage.Identity}");
            workloadPackage.Extract(packageContentPath, Enumerable.Empty<string>());
            string workloadManifestJsonPath = Directory.GetFiles(packageContentPath, "WorkloadManifest.json").FirstOrDefault();

            if (string.IsNullOrWhiteSpace(workloadManifestJsonPath))
            {
                throw new FileNotFoundException($"Unable to locate WorkloadManifest.json under '{packageContentPath}'.");
            }

            WorkloadManifest manifest = WorkloadManifestReader.ReadWorkloadManifest(File.OpenRead(workloadManifestJsonPath));

            List<TaskItem> swixProjects = new();
            
            foreach (WorkloadDefinition workloadDefinition in manifest.Workloads.Values)
            {
                VisualStudioComponent component = VisualStudioComponent.Create(manifest, workloadDefinition);
                swixProjects.Add(component.Generate(Path.Combine(SourceDirectory, $"{workloadDefinition.Id}.{manifest.Version}.0")));
            }

            return swixProjects;
        }
    }
}
