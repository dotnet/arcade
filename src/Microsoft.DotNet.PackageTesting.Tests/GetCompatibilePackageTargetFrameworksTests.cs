// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.DotNet.PackageTesting.Tests
{
    public class GetCompatibilePackageTargetFrameworksTests
    {
        public GetCompatibilePackageTargetFrameworksTests()
        {
            GetCompatiblePackageTargetFrameworks.Initialize();
        }

        public static IEnumerable<object[]> PackageTFMData => new List<object[]>
        {
            // single target framework in package
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard20}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard20, FrameworkConstants.CommonFrameworks.NetCoreApp20, FrameworkConstants.CommonFrameworks.Net463, FrameworkConstants.CommonFrameworks.Net461, FrameworkConstants.CommonFrameworks.Net462} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp20}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp20} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp21}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp21} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.Net461}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.Net461} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.Net45}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.Net45} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp30}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp30} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp31}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp31} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard21}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard21, FrameworkConstants.CommonFrameworks.NetCoreApp30 } },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard12}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard12, FrameworkConstants.CommonFrameworks.Net451 } },

            // two target frameworks in package
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard20, FrameworkConstants.CommonFrameworks.Net461}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard20, FrameworkConstants.CommonFrameworks.NetCoreApp20, FrameworkConstants.CommonFrameworks.Net463, FrameworkConstants.CommonFrameworks.Net461, FrameworkConstants.CommonFrameworks.Net462} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard20, FrameworkConstants.CommonFrameworks.NetCoreApp30}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetStandard20, FrameworkConstants.CommonFrameworks.NetCoreApp30, FrameworkConstants.CommonFrameworks.NetCoreApp20, FrameworkConstants.CommonFrameworks.Net463, FrameworkConstants.CommonFrameworks.Net461, FrameworkConstants.CommonFrameworks.Net462} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp30, FrameworkConstants.CommonFrameworks.Net461}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp30, FrameworkConstants.CommonFrameworks.Net461} },
            new object[] {  new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp30, FrameworkConstants.CommonFrameworks.Net50}, new List<NuGetFramework> { FrameworkConstants.CommonFrameworks.NetCoreApp30, FrameworkConstants.CommonFrameworks.Net50} },
        };

        [Theory]
        [MemberData(nameof(PackageTFMData))]
        public void GetCompatibleFrameworks(List<NuGetFramework> packageFrameworks, List<NuGetFramework> expectedTestFrameworks)
        {
            List<NuGetFramework> actualTestFrameworks = GetCompatiblePackageTargetFrameworks.GetTestFrameworks(packageFrameworks);
            CollectionsEqual(expectedTestFrameworks, actualTestFrameworks);
        }

        private static void CollectionsEqual<T>(IEnumerable<T> T1, IEnumerable<T> T2)
        {
            foreach (var item in T1)
            {
                Assert.Contains(item, T2);
            }
            foreach (var item in T2)
            {
                Assert.Contains(item, T1);
            }
        }
    }
}
