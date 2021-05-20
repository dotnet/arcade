// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageTesting
{
    public class PackageAsset
    {
        public NuGetFramework TargetFramework { get; set; }
        public string Rid { get; set; }
        public string PackagePath { get; set; }
        public AssetType AssetType { get; set; }

        public PackageAsset(NuGetFramework targetFramework, string runtimeIdentifier, string packagePath, AssetType assetType)
        {
            TargetFramework = targetFramework;
            Rid = runtimeIdentifier;
            PackagePath = packagePath;
            AssetType = assetType;
        }
    }

    public enum AssetType
    {
        RefAsset = 0,
        LibAsset = 1,
        RuntimeAsset = 2
    }
}
