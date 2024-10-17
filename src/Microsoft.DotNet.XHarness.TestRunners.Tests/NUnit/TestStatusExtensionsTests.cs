// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.XHarness.Common;
using Microsoft.DotNet.XHarness.TestRunners.NUnit;
using NUnit.Framework.Interfaces;
using Xunit;

#nullable enable
namespace Microsoft.DotNet.XHarness.TestRunners.Tests.NUnit;

public class TestStatusExtensionsTests
{
    public class TestStatusExtensionsTestData
    {
        public static IEnumerable<object[]> ToXmlResultValueTests
        {
            get
            {
                // NUnit v2
                yield return new object[] { TestStatus.Failed, XmlResultJargon.NUnitV2, "Failure", };
                yield return new object[] { TestStatus.Inconclusive, XmlResultJargon.NUnitV2, "Inconclusive", };
                yield return new object[] { TestStatus.Passed, XmlResultJargon.NUnitV2, "Success" };
                yield return new object[] { TestStatus.Skipped, XmlResultJargon.NUnitV2, "Ignored" };
                yield return new object[] { TestStatus.Warning, XmlResultJargon.NUnitV2, "Failure" };

                // NUnit v3
                yield return new object[] { TestStatus.Failed, XmlResultJargon.NUnitV3, "Failed", };
                yield return new object[] { TestStatus.Inconclusive, XmlResultJargon.NUnitV3, "Inconclusive", };
                yield return new object[] { TestStatus.Passed, XmlResultJargon.NUnitV3, "Passed" };
                yield return new object[] { TestStatus.Skipped, XmlResultJargon.NUnitV3, "Skipped" };
                yield return new object[] { TestStatus.Warning, XmlResultJargon.NUnitV3, "Failed" };

                // xunit
                yield return new object[] { TestStatus.Failed, XmlResultJargon.xUnit, "Fail", };
                yield return new object[] { TestStatus.Inconclusive, XmlResultJargon.xUnit, "Skip", };
                yield return new object[] { TestStatus.Passed, XmlResultJargon.xUnit, "Pass" };
                yield return new object[] { TestStatus.Skipped, XmlResultJargon.xUnit, "Skip" };
                yield return new object[] { TestStatus.Warning, XmlResultJargon.xUnit, "Fail" };
            }
        }

        [Theory]
        [MemberData(nameof(ToXmlResultValueTests), MemberType = typeof(TestStatusExtensionsTestData))]
        public void IsExcludedAsAssembly(TestStatus status, XmlResultJargon jargon, string expectedResult)
            => Assert.Equal(status.ToXmlResultValue(jargon), expectedResult);
    }
}
