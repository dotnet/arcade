// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    public class PackageValidationTests
    {
        private static string testAssemblyName = "PackageValidationTests.dll";
        private static string packageVersion = "1.0.0";

        public PackageValidationTests()
        {
            PackageValidation.Helpers.Initialize(Helpers.allTargetFrameworks);
            CreatePackagesData(PackageDataFiles);
        }

        public static IEnumerable<string[]> PackageDataFiles => new List<string[]>
        {
            new string[] {  $"ref/netcoreapp3.0/{testAssemblyName}", $"lib/netcoreapp3.0/{testAssemblyName}" }
        };

        public static IEnumerable<object[]> PackageData;
        
        private static IEnumerable<Package> CreatePackagesData(IEnumerable<string[]> filePaths)
        {
            var packages = new List<Package>();
            foreach (IEnumerable<string> files in filePaths)
            {
                Package package = new(testAssemblyName, packageVersion, files, null, null);
                packages.Add(package);
            }
            return packages;
        }

        [Theory]
        [MemberData(nameof(PackageData))]
        public void ValidRuntimeAssetsForCompileTimeAssets(Package package)
        {
            new ValidatePackage().ValidateCompileAgainstTimeRuntimeTfms(package);
        }
    }
}
