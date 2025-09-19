// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using Microsoft.Arcade.Test.Common;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Tests
{
    public abstract class TestBase
    {
        /// <summary>
        /// This is a version of Arcade that contains updated tasks for creating WiX packs that support
        /// signing MSIs built using WiX v5.
        /// </summary>
        public static readonly string MicrosoftDotNetBuildTasksInstallersPackageVersion = "10.0.0-beta.25420.109";

        public static readonly string BaseIntermediateOutputPath = Path.Combine(AppContext.BaseDirectory, "obj", Path.GetFileNameWithoutExtension(Path.GetTempFileName()));
        public static readonly string BaseOutputPath = Path.Combine(AppContext.BaseDirectory, "bin", Path.GetFileNameWithoutExtension(Path.GetTempFileName()));

        public static readonly string MsiOutputPath = Path.Combine(BaseOutputPath, "msi");

        public static readonly string TestAssetsPath = Path.Combine(AppContext.BaseDirectory, "testassets");

        public static readonly string WixToolsetPath = Path.Combine(TestAssetsPath, "wix");

        public static readonly string PackageRootDirectory = Path.Combine(BaseIntermediateOutputPath, "pkg");

        public static readonly string TestOutputRoot = Path.Combine(AppContext.BaseDirectory, "TEST_OUTPUT");

        /// <summary>
        /// Returns a new, random directory for a test case.
        /// </summary>
        public string GetTestCaseDirectory() =>
            Path.Combine(TestOutputRoot, Path.GetFileNameWithoutExtension(Path.GetRandomFileName()));
    }
}
