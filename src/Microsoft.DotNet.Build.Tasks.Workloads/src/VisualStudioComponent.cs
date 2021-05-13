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
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Represents a Visual Studio component or component group.
    /// </summary>
    public class VisualStudioComponent
    {
        /// <summary>
        /// The component category.
        /// </summary>
        public string Category
        {
            get;
        } = ".NET";

        /// <summary>
        /// The description of the component, displayed as a tooltip inside the UI.
        /// </summary>
        public string Description
        {
            get;
        }

        /// <summary>
        /// Gets whether this component has any dependencies.
        /// </summary>
        public bool HasDependencies => Dependencies.Count > 0;

        /// <summary>
        /// The component name (ID).
        /// </summary>
        public string Name
        {
            get;
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
        /// The title of the component to display in the installer UI, e.g. the individual component tab.
        /// </summary>
        public string Title
        {
            get;
        }

        /// <summary>
        /// The version of the component.
        /// </summary>
        public Version Version
        {
            get;
        }

        private ICollection<VisualStudioDependency> Dependencies = new List<VisualStudioDependency>();

        public VisualStudioComponent(string name, string description, string title, Version version, ITaskItem[] shortNames,
            string category)
        {
            Name = name;
            Description = description;
            Title = title;
            Version = version;
            ShortNames = shortNames;
            Category = category;
        }

        /// <summary>
        /// Add a component dependency using the provided name and version.
        /// </summary>
        /// <param name="name">The name (ID) of the dependency.</param>
        /// <param name="version">The version of the dependency.</param>
        public void AddDependency(string name, Version version)
        {
            AddDependency(new VisualStudioDependency(name, version));
        }

        /// <summary>
        /// Add a component dependency using the specified <see cref="VisualStudioDependency"/>.
        /// </summary>
        /// <param name="dependency">The dependency to add to this component.</param>
        public void AddDependency(VisualStudioDependency dependency)
        {
            Dependencies.Add(dependency);
        }

        /// <summary>
        /// Add a component dependency using the specified item.
        /// </summary>
        /// <param name="dependency">The dependency to add to this component.</param>
        public void AddDependency(ITaskItem dependency)
        {
            AddDependency(new VisualStudioDependency(dependency.ItemSpec.Replace(ShortNames), new Version(dependency.GetMetadata(Metadata.Version))));
        }

        /// <summary>
        /// Add a dependency using the specified workload pack.
        /// </summary>
        /// <param name="pack">The dependency to add to this component.</param>
        public void AddDependency(WorkloadPack pack)
        {
            AddDependency($"{pack.Id.ToString().Replace(ShortNames)}.{pack.Version}", new NuGetVersion(pack.Version).Version);
        }

        public IEnumerable<VisualStudioDependency> GetAliasedDependencies(WorkloadPack pack)
        {
            foreach (var rid in pack.AliasTo.Keys)
            {
                switch (rid)
                {
                    case "win-x86":
                    case "win-x64":
                        yield return new VisualStudioDependency($"{pack.AliasTo[rid].ToString().Replace(ShortNames)}.{pack.Version}", new NuGetVersion(pack.Version).Version);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// Generate a SWIX project for the component in the specified folder.
        /// </summary>
        /// <param name="projectPath">The path of the SWIX project to generate.</param>
        /// <returns>An item describing the generated SWIX project.</returns>
        public TaskItem Generate(string projectPath)
        {
            string componentSwr = EmbeddedTemplates.Extract("component.swr", projectPath);
            string componentResSwr = EmbeddedTemplates.Extract("component.res.swr", projectPath);
            string componentSwixProj = EmbeddedTemplates.Extract("component.swixproj", projectPath, $"{Name}.{Version.ToString(2)}.swixproj");

            Dictionary<string, string> replacementTokens = GetReplacementTokens();
            Utils.StringReplace(componentSwr, replacementTokens, Encoding.UTF8);
            Utils.StringReplace(componentResSwr, replacementTokens, Encoding.UTF8);

            using StreamWriter swrWriter = File.AppendText(componentSwr);

            foreach (VisualStudioDependency dependency in Dependencies)
            {
                // SWIX is indentation sensitive. The dependencies should be written as 
                //
                // vs.dependencies
                //   vs.dependency id=<packageID>
                //                 version=[1.2.3.4]

                swrWriter.WriteLine($"  vs.dependency id={dependency.Id}");
                swrWriter.WriteLine($"                version=[{dependency.Version}]");
                swrWriter.WriteLine($"                behaviors=IgnoreApplicabilityFailures");
            }

            return new TaskItem(componentSwixProj);
        }

        private Dictionary<string, string> GetReplacementTokens()
        {
            return new Dictionary<string, string>()
            {
                {"__VS_PACKAGE_NAME__", Name },
                {"__VS_PACKAGE_VERSION__", Version.ToString() },
                {"__VS_COMPONENT_TITLE__", Title },
                {"__VS_COMPONENT_DESCRIPTION__", Description },
                {"__VS_COMPONENT_CATEGORY__", Category ?? ".NET" }
            };
        }

        /// <summary>
        /// Creates a <see cref="VisualStudioComponent"/> using a workload definition.
        /// </summary>
        /// <param name="manifest"></param>
        /// <param name="workload"></param>
        /// <param name="componentVersions"></param>
        /// <param name="shortNames"></param>
        /// <param name="shortNameMetadata"></param>
        /// <param name="componentResources"></param>
        /// <returns></returns>
        public static VisualStudioComponent Create(TaskLoggingHelper log, WorkloadManifest manifest, WorkloadDefinition workload, ITaskItem[] componentVersions,
            ITaskItem[] shortNames, ITaskItem[] componentResources, ITaskItem[] missingPacks)
        {
            log?.LogMessage("Creating Visual Studio component");
            string workloadId = $"{workload.Id}";

            // If there's an explicit version mapping we use that, otherwise we fall back to the manifest version
            // and normalize it since it can have semantic information and Visual Studio components do not support that.
            ITaskItem versionItem = componentVersions?.Where(v => string.Equals(v.ItemSpec, workloadId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
            Version version = (versionItem != null) && !string.IsNullOrWhiteSpace(versionItem.GetMetadata(Metadata.Version))
                ? new Version(versionItem.GetMetadata(Metadata.Version))
                : (new NuGetVersion(manifest.Version)).Version;

            ITaskItem resourceItem = componentResources?.Where(
                r => string.Equals(r.ItemSpec, workloadId, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

            // Workload definitions do not have separate title/description fields so the only option
            // is to default to the workload description for both.
            string title = resourceItem?.GetMetadata(Metadata.Title) ?? workload.Description;
            string description = resourceItem?.GetMetadata(Metadata.Description) ?? workload.Description;
            string category = resourceItem?.GetMetadata(Metadata.Category) ?? ".NET";

            VisualStudioComponent component = new(Utils.ToSafeId(workloadId), description,
                title, version, shortNames, category);

            IEnumerable<string> missingPackIds = missingPacks.Select(p => p.ItemSpec);
            log?.LogMessage(MessageImportance.Low, $"Missing packs: {string.Join(", ", missingPackIds)}");

            // Visual Studio is case-insensitive. 
            IEnumerable<WorkloadPackId> packIds = workload.Packs.Where(p => !missingPackIds.Contains($"{p}", StringComparer.OrdinalIgnoreCase));
            log?.LogMessage(MessageImportance.Low, $"Packs: {string.Join(", ", packIds.Select(p=>$"{p}"))}");

            foreach (WorkloadPackId packId in packIds)
            {
                log?.LogMessage(MessageImportance.Low, $"Adding component dependency for {packId} ");
                component.AddDependency(manifest.Packs[packId]);
            }

            return component;
        }
    }
}
