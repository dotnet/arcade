// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    /// <summary>
    /// Metadata names for MSBuild ITaskItem.
    /// </summary>
    internal static class Metadata
    {
        public static readonly string AliasTo = nameof(AliasTo);
        public static readonly string Category = nameof(Category);
        public static readonly string Description = nameof(Description);
        public static readonly string Filename = nameof(Filename);
        public static readonly string FullPath = nameof(FullPath);
        public static readonly string JsonProperties = nameof(JsonProperties);
        public static readonly string MsiVersion = nameof(MsiVersion);
        public static readonly string Platform = nameof(Platform);
        public static readonly string RelativeDir = nameof(RelativeDir);
        public static readonly string Replacement = nameof(Replacement);        
        public static readonly string PackageProject = nameof(PackageProject);
        public static readonly string SdkFeatureBand = nameof(SdkFeatureBand);
        public static readonly string ShortName = nameof(ShortName);
        public static readonly string SourcePackage = nameof(SourcePackage);
        public static readonly string SwixPackageId = nameof(SwixPackageId);
        public static readonly string SwixProject = nameof(SwixProject);
        public static readonly string Title = nameof(Title);
        public static readonly string Version = nameof(Version);

        /// <summary>
        /// Metadata used by tasks generating MSIs to specify the path of .wixobj files produced by
        /// the compiler.
        /// </summary>
        public static readonly string WixObj = nameof(WixObj);
    }
}
