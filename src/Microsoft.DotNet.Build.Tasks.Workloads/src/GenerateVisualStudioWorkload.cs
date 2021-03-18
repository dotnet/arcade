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
    public class GenerateVisualStudioWorkload : GenerateTaskBase
    {
        
        /// <summary>
        /// A set of workload manifest files.
        /// </summary>
        public ITaskItem[] WorkloadManifests
        {
            get;
            set;
        }

        /// <summary>
        /// Set of packages containing workload manifests.
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

        public IEnumerable<ITaskItem> ProcessWorkloadManifest(string workloadManifestPackage)
        {
            NugetPackage workloadPackage = new NugetPackage(workloadManifestPackage, Log);

            string packageContentPath = Path.Combine(PackageDirectory, workloadPackage.Id, workloadPackage.Version.ToNormalizedString());
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
                ComponentPackage package = ComponentPackage.Create(manifest, workloadDefinition);
                swixProjects.Add(package.Generate(Path.Combine(SourceDirectory, $"{workloadDefinition.Id}.{manifest.Version}.0")));
            }

            return swixProjects;
        }
    }
}
