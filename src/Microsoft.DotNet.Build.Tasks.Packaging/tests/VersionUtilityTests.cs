// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Packaging.Tests
{
    public class VersionUtilityTests
    {
        public static IEnumerable<object[]> IsCompatibleTestData()
        {
            yield return new object[] { new Version(4, 0, 0, 0), new Version(4, 0, 0, 0), true };
            yield return new object[] { new Version(4, 0, 0, 0), new Version(5, 0, 0, 0), false };
            yield return new object[] { new Version(4, 0, 0, 0), new Version(3, 0, 0, 0), false };
            yield return new object[] { new Version(4, 0, 0, 0), new Version(4, 1, 0, 0), false };
            yield return new object[] { new Version(3, 8, 0, 0), new Version(3, 7, 0, 0), false };
            yield return new object[] { new Version(4, 0, 0, 0), new Version(4, 0, 1, 0), true };
            yield return new object[] { new Version(4, 0, 1, 0), new Version(4, 0, 0, 0), false };
            yield return new object[] { new Version(4, 0, 0, 0), new Version(4, 0, 0, 1), true };
            yield return new object[] { new Version(4, 0, 0, 1), new Version(4, 0, 0, 0), false };
            yield return new object[] { new Version(4, 0, 3, 2), new Version(4, 0, 4, 3), true };
            yield return new object[] { new Version(4, 0, 3, 2), new Version(4, 0, 4, 0), true };
        }

        [Theory]
        [MemberData(nameof(IsCompatibleTestData))]
        public void IsCompatibleApiVersionTest(Version referenceVersion, Version candidateVersion, bool shouldBeCompatible)
        {
            bool isCompatible = VersionUtility.IsCompatibleApiVersion(referenceVersion, candidateVersion);
            if (shouldBeCompatible)
            {
                Assert.True(isCompatible, $"Version {candidateVersion.ToString()} should be compatible with version {referenceVersion.ToString()}");
            }
            else
            {
                Assert.False(isCompatible, $"Version {candidateVersion.ToString()} should not be compatible with version {referenceVersion.ToString()}");
            }
        }
    }
}
