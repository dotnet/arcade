// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using Xunit.Sdk;

namespace Microsoft.DotNet.XUnitExtensions.Tests
{
    [TestCaseOrderer("Microsoft.DotNet.XUnitExtensions.Tests.AlphabeticalOrderer", "Microsoft.DotNet.XUnitExtensions.Tests")]
    public class ActiveIssueTests
    {
        [Fact]
        [ActiveIssue("https://github.com/repo/123")]
        public void CheckIfSkipsFact()
        {
            Assert.False(true, "Should have been skipped.");
        }

        [Fact]
        [ActiveIssue("https://github.com/repo/456")]
        public void CheckIfSkipsConditionalFact()
        {
            Assert.False(true, "Should have been skipped.");
        }
    }
}
