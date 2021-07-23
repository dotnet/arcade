// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.PackageTesting
{
    public class NupkgParser
    {
        public static Package CreatePackageObject(string packagePath)
        {
            PackageArchiveReader nupkgReader = new PackageArchiveReader(packagePath);
            NuspecReader nuspecReader = nupkgReader.NuspecReader;

            string packageId = nuspecReader.GetId();
            string version = nuspecReader.GetVersion().ToString();

            NuGetFramework[] dependencyFrameworks = nuspecReader.GetDependencyGroups().Select(dg => dg.TargetFramework).Where(tfm => tfm != null).ToArray();

            return new Package(packageId, version, nupkgReader.GetFiles()?.Where(t => t.EndsWith(packageId + ".dll")), dependencyFrameworks);
        }
    }
}
