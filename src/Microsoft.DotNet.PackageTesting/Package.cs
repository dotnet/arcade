// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System.Collections.Generic;

namespace Microsoft.DotNet.PackageTesting
{
    public class Package
    {
        public List<PackageAsset> PackageAssets { get; set; }
        public string PackageId { get; set; }
        public string Version { get; set; }
        public Dictionary<NuGetFramework, List<PackageDependency>> PackageDependencies { get; set; }
        public Package(string packageId, string version, List<PackageAsset> packageAssets, Dictionary<NuGetFramework, List<PackageDependency>> packageDependencies)
        {
            PackageId = packageId;
            Version = version;
            PackageAssets = packageAssets;
            PackageDependencies = packageDependencies;
        }
    }
}
