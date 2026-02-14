// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Defines default values that can be used to when creating workload artifacts.
    /// </summary>
    internal static class DefaultValues
    {
        /// <summary>
        /// Maximum size of an MSI in bytes.
        /// </summary>
        /// <remarks>
        /// Workload MSIs are distributed in NuGet packages and cannot exceed the maximum size of a NuGet package (250 MB). The limit
        /// is set to 245 MB to account for package metadata, signatures, etc.
        /// </remarks>
        public const int MaxMsiSize = 256901120;

        /// <summary>
        /// Default component group identifier used when harvesting a directory.
        /// </summary>
        public const string DefaultComponentGroupName = "CG_PackageContents";

        /// <summary>
        /// Prefix used in Visual Studio for SWIX based package group.
        /// </summary>
        public const string PackageGroupPrefix = "PackageGroup";

        /// <summary>
        /// The default category to assign to a SWIX component. The value is used
        /// to group individual components in Visual Studio Installer.
        /// </summary>
        public static readonly string ComponentCategory = ".NET";

        /// <summary>
        /// The default value to assign to the Manufacturer property of an MSI.
        /// </summary>
        public const string Manufacturer = "Microsoft Corporation";

        /// <summary>
        /// Default Feature ID to reference when harvesting files.
        /// </summary>
        public const string PackageContentsFeatureId = "F_PackageContents";

        public static readonly string x86 = "x64";
        public static readonly string x64 = "x64";
        public static readonly string arm64 = "arm64";
        public static readonly string Neutral = "neutral";

        /// <summary>
        /// A value indicating that the SWIX project creates an MSI package for a workload manifest. 
        /// </summary>
        public static readonly string PackageTypeMsiManifest = "msi-manifest";

        /// <summary>
        /// A value indicating that the SWIX project creates an MSI package for a workload pack. 
        /// </summary>
        public static readonly string PackageTypeMsiPack = "msi-pack";

        /// <summary>
        /// A value indicating that the SWIX project creates a component package for a workload. 
        /// </summary>
        public static readonly string PackageTypeComponent = "component";

        /// <summary>
        /// A value indicating that the SWIX project creates a package group for a workload manifest. 
        /// </summary>
        public static readonly string PackageTypeManifestPackageGroup = "manifest-package-group";

        /// <summary>
        /// A value indicating that the SWIX project creates a package group for a workload manifest. 
        /// </summary>
        public static readonly string PackageTypeWorkloadSetPackageGroup = "workloadset-package-group";

        /// <summary>
        /// A value indicating that the SWIX project creates an MSI package for a workload set.
        /// </summary>
        public static readonly string PackageTypeMsiWorkloadSet = "msi-workload-set";

        /// <summary>
        /// A value indicating the MSI represents a workload manifest.
        /// </summary>
        public static readonly string ManifestMsi = "manifest";

        /// <summary>
        /// A value indicating the MSI represents a workload pack.
        /// </summary>
        public static readonly string WorkloadPackMsi = "pack";

        /// <summary>
        /// A value indicating the MSI represents a workload set.
        /// </summary>
        public static readonly string WorkloadSetMsi = "workload-set";

        /// <summary>
        /// A value indicating the MSI represents a workload pack group.
        /// </summary>
        public static readonly string WorkloadPackGroupMsi = "pack-group";
    }
}
