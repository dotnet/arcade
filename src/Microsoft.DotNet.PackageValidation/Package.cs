// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.PackageValidation
{
    public class Package
    {
        private ManagedCodeConventions _conventions;

        public Package(string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, List<PackageDependency>> packageDependencies, string runtimeFile)
        {
            PackageId = packageId;
            Version = version;
            PackageDependencies = packageDependencies;
            PackageAssets.Load(packageAssets);

            RuntimeGraph runtimeGraph = null;
            if (!string.IsNullOrEmpty(runtimeFile))
            {
                runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeFile);
            }
            _conventions = new ManagedCodeConventions(runtimeGraph);

            RefAssets = PackageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);
            LibAssets = PackageAssets.FindItems(_conventions.Patterns.CompileLibAssemblies);
            RuntimeSpecificAssets = PackageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies).Where(t => t.Path.StartsWith("runtimes"));
            RuntimeAssets = PackageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies);
            Rids = RuntimeSpecificAssets?.Select(t => (string)t.Properties["rid"]);
            FrameworksInPackage = PackageAssets.FindItems(_conventions.Patterns.AnyTargettedFile).Select(t => (NuGetFramework)t.Properties["tfm"]);
        }

        public ContentItemCollection PackageAssets { get; set; } = new ContentItemCollection();

        public string PackageId { get; set; }

        public string Version { get; set; }

        public Dictionary<NuGetFramework, List<PackageDependency>> PackageDependencies { get; set; }

        public IEnumerable<ContentItem> CompileAssets => RefAssets != null ? RefAssets : LibAssets;

        public IEnumerable<ContentItem> RefAssets { get; private set; }

        public IEnumerable<ContentItem> LibAssets { get; private set; }

        public IEnumerable<ContentItem> RuntimeSpecificAssets { get; private set; }

        public IEnumerable<ContentItem> RuntimeAssets { get; private set; }

        public bool HasRefAssemblies => RefAssets != null;

        public IEnumerable<string> Rids { get; private set; }

        public IEnumerable<NuGetFramework> FrameworksInPackage { get; private set; }

        public ContentItem FindBestRuntimeAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            return PackageAssets.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items.FirstOrDefault();
        }

        public ContentItem FindBestRuntimeAssetForFrameworkAndRuntime(NuGetFramework framework, string rid)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFrameworkAndRuntime(framework, rid);
            return PackageAssets.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items.FirstOrDefault();
        }

        public ContentItem FindBestCompileAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            if (RefAssets != null)
            {
                return PackageAssets.FindBestItemGroup(managedCriteria,
                    _conventions.Patterns.CompileRefAssemblies)?.Items.FirstOrDefault(); ;
            }
            else
            {
                return PackageAssets.FindBestItemGroup(managedCriteria,
                    _conventions.Patterns.CompileLibAssemblies)?.Items.FirstOrDefault(); ;
            }
        }
    }
}
