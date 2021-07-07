// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageTesting
{
    public class Package
    {
        public Package(string packageId, string version, IEnumerable<string> packageAssetPaths)
        {
            PackageId = packageId;
            Version = version;

            ContentItemCollection packageAssets = new();
            packageAssets.Load(packageAssetPaths);
            ManagedCodeConventions conventions = new ManagedCodeConventions(null);

            IEnumerable<ContentItem> RefAssets = packageAssets.FindItems(conventions.Patterns.CompileRefAssemblies);
            IEnumerable<ContentItem> LibAssets = packageAssets.FindItems(conventions.Patterns.CompileLibAssemblies);
            IEnumerable<ContentItem> CompileAssets = RefAssets.Any() ? RefAssets : LibAssets;
            List<NuGetFramework> FrameworksInPackageList = CompileAssets.Select(t => (NuGetFramework)t.Properties["tfm"]).ToList();

            IEnumerable<ContentItem> RuntimeAssets = packageAssets.FindItems(conventions.Patterns.RuntimeAssemblies);
            FrameworksInPackageList.AddRange(RuntimeAssets.Select(t => (NuGetFramework)t.Properties["tfm"]).Distinct());
            FrameworksInPackage = FrameworksInPackageList.Distinct();
        }

        public string PackageId { get; set; }
        public string Version { get; set; }
        public IEnumerable<NuGetFramework> FrameworksInPackage { get; private set; }
    }
}
