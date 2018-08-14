// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.SignTool.Tests
{
    [System.AttributeUsage(System.AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
    sealed class PathConfiguration : System.Attribute
    {
        public string PackageInstallationPath { get; }
        public string MSBuildPath { get; }

        public PathConfiguration(string installPath, string msBuildPath)
        {
            PackageInstallationPath = installPath;
            MSBuildPath = msBuildPath;
        }
    }
}
