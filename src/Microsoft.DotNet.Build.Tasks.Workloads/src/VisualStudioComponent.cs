// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
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
        /// The component name (ID).
        /// </summary>
        public string Name
        {
            get;
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

        public VisualStudioComponent(string name, string description, string title, Version version)
        {
            Name = name;
            Description = description;
            Title = title;
            Version = version;
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
            AddDependency(new VisualStudioDependency(dependency.ItemSpec, new Version(dependency.GetMetadata("Version"))));
        }

        /// <summary>
        /// Add a dependency using the specified workload pack.
        /// </summary>
        /// <param name="dependency">The dependency to add to this component.</param>
        public void AddDependency(WorkloadPack dependency)
        {
            AddDependency($"{dependency.Id}", new NuGetVersion(dependency.Version).Version);
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
                {"__VS_COMPONENT_CATEGORY__", Category }
            };
        }

        /// <summary>
        /// Creates a <see cref="VisualStudioComponent"/> using a workload definition.
        /// </summary>
        /// <param name="Definition"></param>
        /// <param name="packs"></param>
        /// <returns></returns>
        public static VisualStudioComponent Create(WorkloadManifest manifest, WorkloadDefinition definition)
        {
            VisualStudioComponent package = new(Utils.ToSafeId(definition.Id.ToString()), definition.Description,
                definition.Description, new Version($"{manifest.Version}.0"));

            foreach (WorkloadPackId packId in definition.Packs)
            {
                package.AddDependency(manifest.Packs[packId]);
            }

            return package;
        }
    }
}
