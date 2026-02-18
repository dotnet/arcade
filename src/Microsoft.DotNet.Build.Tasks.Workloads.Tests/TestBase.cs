// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.Build.Tasks.Workloads.Msi;
using FluentAssertions;

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

        protected static void ValidateInstallationRecord(IEnumerable<RegistryRow> registryKeys,
            string installationRecordKey, string expectedProviderKey, string expectedProductCode, string expectedUpgradeCode,
            string expectedProductVersion,
            string expectedProductLanguage = "#1033")
        {
            registryKeys.Should().Contain(r => r.Key == installationRecordKey &&
                r.Root == 2 &&
                r.Name == "DependencyProviderKey" &&
                r.Value == expectedProviderKey);
            registryKeys.Should().Contain(r => r.Key == installationRecordKey &&
                r.Root == 2 &&
                r.Name == "ProductCode" &&
                string.Equals(r.Value, expectedProductCode, StringComparison.OrdinalIgnoreCase));
            registryKeys.Should().Contain(r => r.Key == installationRecordKey &&
                r.Root == 2 &&
                r.Name == "UpgradeCode" &&
                string.Equals(r.Value, expectedUpgradeCode, StringComparison.OrdinalIgnoreCase));
            registryKeys.Should().Contain(r => r.Key == installationRecordKey &&
                r.Root == 2 &&
                r.Name == "ProductVersion" &&
                r.Value == expectedProductVersion);
            registryKeys.Should().Contain(r => r.Key == installationRecordKey &&
                r.Root == 2 &&
                r.Name == "ProductLanguage" &&
                r.Value == expectedProductLanguage);
        }

        protected static void ValidateDependencyProviderKey(IEnumerable<RegistryRow> registryKeys, string dependencyProviderKey)
        {
            // Dependency provider entries references the ProductVersion and ProductName properties. These
            // properties are set by the installer service at install time.
            registryKeys.Should().Contain(r => r.Key == dependencyProviderKey &&
                    r.Root == -1 &&
                    r.Name == "Version" &&
                    r.Value == "[ProductVersion]");
            registryKeys.Should().Contain(r => r.Key == dependencyProviderKey &&
                r.Root == -1 &&
                r.Name == "DisplayName" &&
                r.Value == "[ProductName]");
        }
    }
}
