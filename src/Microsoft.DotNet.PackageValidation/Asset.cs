// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.DotNet.PackageValidation
{
    public class Asset
    {
        public NuGetFramework TargetFramework { get; set; }
        public string Rid { get; set; }
        public string PackagePath { get; set; }
        public AssetType AssetType { get; set; }

        public Asset(NuGetFramework targetFramework, string runtimeIdentifier, string packagePath1, AssetType assetType)
        {
            TargetFramework = targetFramework;
            Rid = runtimeIdentifier;
            PackagePath = packagePath1;
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
