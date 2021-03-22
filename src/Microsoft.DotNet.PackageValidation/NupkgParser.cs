// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.PackageValidation
{
    public class NupkgParser
    {
        public static Package CreatePackageObject(string packagePath)
        {
            Dictionary<NuGetFramework, List<PackageDependency>> packageDependencies = new Dictionary<NuGetFramework, List<PackageDependency>>();
             
            PackageArchiveReader nupkgReader = new PackageArchiveReader(packagePath);
            NuspecReader nuspecReader = nupkgReader.NuspecReader;

            string title = nuspecReader.GetTitle();
            string version = nuspecReader.GetVersion().ToString();
            IEnumerable<PackageDependencyGroup> dependencyGroups = nuspecReader.GetDependencyGroups();

            foreach (var item in dependencyGroups)
            {
                packageDependencies.Add(item.TargetFramework, item.Packages.ToList());
            }

            return new Package(title, version, nupkgReader.GetFiles().Where(t => t.EndsWith(title + ".dll" )), packageDependencies);
        }
    }
}
