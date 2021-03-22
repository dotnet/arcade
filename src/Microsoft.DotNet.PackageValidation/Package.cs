// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using NuGet.ContentModel;
using NuGet.Client;
using NuGet.RuntimeModel;
using System;
using System.Linq;

namespace Microsoft.DotNet.PackageValidation
{
    public class Package
    {
        public ContentItemCollection PackageAssets { get; set; } = new ContentItemCollection();
        public string PackageId { get; set; }
        public string Version { get; set; }
        public Dictionary<NuGetFramework, List<PackageDependency>> PackageDependencies { get; set; }

        private ManagedCodeConventions _conventions;

        public Package(string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, List<PackageDependency>> packageDependencies, string runtimeFile = null)
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
        }

        public IEnumerable<ContentItem> GetCompileAssets()
        {
            IEnumerable<ContentItem> refAssemblies = PackageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);

            if (refAssemblies == null)
                return PackageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);

            return refAssemblies;
        }

        public IEnumerable<ContentItem> GetRefAssets()
        {
            return PackageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);
        }

        public IEnumerable<ContentItem> GetLibAssets()
        {
            return PackageAssets.FindItems(_conventions.Patterns.CompileLibAssemblies);
        }

        public IEnumerable<ContentItem> GetRuntimeAssets()
        {
            return PackageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies);
        }

        public IEnumerable<ContentItem> GetRuntimeSpecificAssets()
        {
            return PackageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies).Where(t => t.Path.StartsWith("runtimes"));
        }

        public IEnumerable<string> GetRids()
        {
            return GetRuntimeSpecificAssets().Select(t => (string)t.Properties["rid"]);
        }

        public IEnumerable<NuGetFramework> ListFrameworksInPackage()
        {
            return PackageAssets.FindItems(_conventions.Patterns.AnyTargettedFile).Select(t => (NuGetFramework)t.Properties["tfm"]);
        }
    }

}
