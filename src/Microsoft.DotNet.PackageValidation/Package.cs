// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System.Collections.Generic;

namespace Microsoft.DotNet.PackageValidation
{
    public class Package
    {
        public List<PackageAsset> PackageAssets { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public Dictionary<NuGetFramework, List<PackageDependency>> PackageDependencies { get; set; }
        public Package(string title, string version, List<PackageAsset> packageAssets, Dictionary<NuGetFramework, List<PackageDependency>> packageDependencies)
        {
            Title = title;
            Version = version;
            PackageAssets = packageAssets;
            PackageDependencies = packageDependencies;
        }
    }
}
