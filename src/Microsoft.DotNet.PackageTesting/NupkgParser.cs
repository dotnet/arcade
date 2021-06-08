// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.PackageTesting
{
    public class NupkgParser
    {
        public static Package CreatePackageObject(string packagePath)
        {
            List<PackageAsset> packageAssets = new List<PackageAsset>();
            Dictionary<NuGetFramework, List<PackageDependency>> packageDependencies = new Dictionary<NuGetFramework, List<PackageDependency>>();
             
            PackageArchiveReader nupkgReader = new PackageArchiveReader(packagePath);
            NuspecReader nuspecReader = nupkgReader.NuspecReader;

            string packageId = nuspecReader.GetId();
            string version = nuspecReader.GetVersion().ToString();
            IEnumerable<PackageDependencyGroup> dependencyGroups = nuspecReader.GetDependencyGroups();

            foreach (var item in dependencyGroups)
            {
                packageDependencies.Add(item.TargetFramework, item.Packages.ToList());
            }

            var files = nupkgReader.GetFiles().ToList().Where(t => t.EndsWith(".dll")).Where(t => t.Contains(packageId + ".dll"));
            foreach (var file in files)
            {
                packageAssets.Add(ExtractAssetFromFile(file));
            }

            return new Package(packageId, version, packageAssets, packageDependencies);
        }

        public static PackageAsset ExtractAssetFromFile(string filePath)
        {
            PackageAsset asset = null;
            if (filePath.StartsWith("ref"))
            {
                var stringParts = filePath.Split('/');
                asset = new PackageAsset(NuGetFramework.Parse(stringParts[1]), null, filePath, AssetType.RefAsset);
            }
            else if (filePath.StartsWith("lib"))
            {
                var stringParts = filePath.Split('/');
                asset = new PackageAsset(NuGetFramework.Parse(stringParts[1]), null, filePath, AssetType.LibAsset);

            }
            else if (filePath.StartsWith("runtimes"))
            {
                var stringParts = filePath.Split('/');
                NuGetFramework framework = stringParts.Length > 3 ? NuGetFramework.Parse(stringParts[3]) : null;
                asset = new PackageAsset(framework, stringParts[1], filePath, AssetType.RuntimeAsset);
            }

            return asset;
        }
    }
}
