// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Deployment.DotNet.Releases;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Swix
{
    /// <summary>
    /// Represents a component or component group in Visual Studio Installer.
    /// </summary>
    internal class SwixComponent
    {
        /// <summary>
        /// RIDs supported on Windows. Only pack dependencies that contain these RIDs will be added to the component.
        /// </summary>
        private static readonly string[] s_SupportedRids = new string[] { "win7", "win10", "any", "win", "win-x64", "win-x86", "win-arm64" };

        /// <summary>
        /// Default version to assign to component dependencies.
        /// </summary>
        private static readonly Version s_v1 = new Version("1.0.0.0");

        private List<SwixDependency> _dependencies = new();

        /// <summary>
        /// The component category.
        /// </summary>
        public string Category
        {
            get;
        } = DefaultValues.ComponentCategory;

        /// <summary>
        /// Gets the set of packages and components on which this component depends.
        /// </summary>
        public IEnumerable<SwixDependency> Dependencies => _dependencies;

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
        public bool HasDependencies => _dependencies.Count > 0;

        /// <summary>
        /// When <see langword="true" />, this component represents a component group that is only visible as a top-level 
        /// dependency in the wokloads tab. Otherwise it is a visible component that becomes selectable in the individual components
        /// tab.
        /// </summary>
        public bool IsUiGroup
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
        /// The SDK feature band associated with this component.
        /// </summary>
        public ReleaseVersion SdkFeatureBand
        {
            get;
        }

        /// <summary>
        /// A set of items used to shorten the names and identifiers of setup packages.
        /// </summary>
        public ITaskItem[]? ShortNames
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

        /// <summary>
        /// Creates a new SWIX component.
        /// </summary>
        /// <param name="sdkFeatureBand">The SDK feature band associated with the component.</param>
        /// <param name="name">The component ID.</param>
        /// /// <param name="title">The component title as it appears next to checkboxes on the workload and individual component tabs.</param>
        /// <param name="description">The component description, displayed as a tooltip in the Visual Studio Installer UI.</param>
        /// <param name="version">The version of the component.</param>
        /// <param name="isUiGroup">When <see langword="true"/>, indicates that this component is a component group and
        /// will be hidden on the individual components tab.</param>
        /// <param name="category">The category associated with the component. The value acts as a grouping mechanism on
        /// the individual components tab.</param>
        /// <param name="shortNames">A set of items used to shorten the names and identifiers of setup packages.</param>
        internal SwixComponent(ReleaseVersion sdkFeatureBand, string name, string title, string description, Version version,
            bool isUiGroup, string category, ITaskItem[]? shortNames)
        {
            Name = name;
            Title = title;
            Description = description;
            Version = version;
            IsUiGroup = isUiGroup;
            Category = category;
            SdkFeatureBand = sdkFeatureBand;
            ShortNames = shortNames;
        }

        /// <summary>
        /// Adds a depdency using the specified ID and versions.
        /// </summary>
        /// <param name="id">The SWIX ID of the dependency.</param>
        /// <param name="minVersion">The minimum dependency version.</param>
        /// <param name="maxVersion">The maximum dependency version.</param>
        public void AddDependency(string id, Version? minVersion = null, Version? maxVersion = null)
        {
            _dependencies.Add(new SwixDependency(id, minVersion, maxVersion));
        }

        /// <summary>
        /// Adds a dependency using the specified workload pack.
        /// </summary>
        /// <param name="pack">The workload pack to add as a dependency.</param>
        public void AddDependency(WorkloadPack pack)
        {
            _dependencies.Add(new SwixDependency($"{pack.Id.ToString().Replace(ShortNames)}.{pack.Version}", new NuGetVersion(pack.Version).Version, maxVersion: null));
        }

        /// <summary>
        /// Creates a SWIX component representing a workload definition.
        /// </summary>
        /// <param name="sdkFeatureBand">The SDK featureband associated with the workload manifest.</param>
        /// <param name="workload">The workload definition to use for the component.</param>
        /// <param name="manifest">The workload manifest to which the workload belongs.</param>
        /// <param name="packGroupId">The ID of a workload pack group to add as a dependency instead of individual packs</param>
        /// <param name="componentResources">Additional resources that can be used to override component attributes such
        /// as the title, description, and category.</param>
        /// <param name="shortNames">A set of items used to shorten the names of setup packages.</param>
        /// <returns>A SWIX component.</returns>
        public static SwixComponent Create(ReleaseVersion sdkFeatureBand, WorkloadDefinition workload, WorkloadManifest manifest,
            string? packGroupId,
            ITaskItem[]? componentResources = null, ITaskItem[]? shortNames = null)
        {
            ITaskItem? resourceItem = componentResources?.Where(r => string.Equals(r.ItemSpec, workload.Id)).FirstOrDefault();

            // If no explicit version mapping exists for the workload, the major.minor.patch version of the manifest is used
            // for the component version in Visual Studio Installer.
            Version componentVersion = resourceItem != null && !string.IsNullOrWhiteSpace(resourceItem.GetMetadata(Metadata.Version)) ?
                new Version(resourceItem.GetMetadata(Metadata.Version)) :
                new Version((new ReleaseVersion(manifest.Version)).ToString(3));

            // Since workloads only define a description, if no custom resources were provided, both the title and description of
            // the SWIX component will default to the workload description.
            SwixComponent component = new(sdkFeatureBand, Utils.ToSafeId(workload.Id), 
                resourceItem?.GetMetadata(Metadata.Title) ?? workload.Description ?? throw new Exception(Strings.ComponentTitleCannotBeNull),
                resourceItem?.GetMetadata(Metadata.Description) ?? workload.Description ?? throw new Exception(Strings.ComponentDescriptionCannotBeNull), 
                componentVersion, workload.IsAbstract,
                resourceItem?.GetMetadata(Metadata.Category) ?? DefaultValues.ComponentCategory,
                shortNames);

            // If the workload extends other workloads, we add those as component dependencies before
            // processing direct pack dependencies.
            foreach (WorkloadId dependency in workload.Extends ?? Enumerable.Empty<WorkloadId>())
            {
                component.AddDependency(Utils.ToSafeId(dependency), s_v1);
            }

            // TODO: Check for missing packs

            if (packGroupId != null)
            {
                //  Add dependency to workload pack group
                component.AddDependency(packGroupId, new NuGetVersion(manifest.Version).Version, maxVersion: null);
            }
            else
            {
                foreach (WorkloadPackId packId in workload.Packs ?? Enumerable.Empty<WorkloadPackId>())
                {
                    // Check whether the pack dependency is aliased to non-Windows RIDs. If so, we can't add a dependency for the pack
                    // because we won't be able to create any installers.
                    if (manifest.Packs.TryGetValue(packId, out WorkloadPack? pack))
                    {
                        if (pack.IsAlias && pack.AliasTo != null && !pack.AliasTo.Keys.Any(rid => s_SupportedRids.Contains(rid)))
                        {
                            continue;
                        }

                        component.AddDependency(manifest.Packs[packId]);
                    }
                }
            }

            return component;
        }
    }
}

#nullable disable
