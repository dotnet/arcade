// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Versioning.Tests
{
    public class GenerateVersioningDateTests
    {
        private readonly ITestOutputHelper _output;

        public GenerateVersioningDateTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicTest()
        {
            var task = new GenerateVersioningDate
            {
                ComparisonDate = "2018-03-11",
                SeedDate = "2018-05-14",
                Padding = 9,
                IncludePadding = true
            };

            Assert.True(task.Execute(), "Task should pass");

            Assert.Equal("0", task.GeneratedRevision);
            Assert.Equal("000000214", task.GeneratedShortDate);
        }

        [Fact]
        public void BasicTestNoLeadingZeros()
        {
            var task = new GenerateVersioningDate
            {
                ComparisonDate = "2018-03-11",
                SeedDate = "2018-05-04",
                Padding = 7,
                IncludePadding = false
            };

            Assert.True(task.Execute(), "Task should pass");

            Assert.Equal("0", task.GeneratedRevision);
            Assert.Equal("204", task.GeneratedShortDate);
        }

        [Fact]
        public void TestWithOfficialBuildId()
        {
            var task = new GenerateVersioningDate
            {
                ComparisonDate = "2017-01-01",
                OfficialBuildId = "20180514-02",
                Padding = 7,
                IncludePadding = true
            };

            Assert.True(task.Execute(), "Task should pass");

            Assert.Equal("02", task.GeneratedRevision);
            Assert.Equal("0001614", task.GeneratedShortDate);
        }

        [Fact]
        public void TestWithOfficialBuildIdNoLeadingZeros()
        {
            var task = new GenerateVersioningDate
            {
                ComparisonDate = "2017-01-01",
                OfficialBuildId = "20180514-02",
                Padding = 7,
                IncludePadding = false
            };

            Assert.True(task.Execute(), "Task should pass");

            Assert.Equal("2", task.GeneratedRevision);
            Assert.Equal("1614", task.GeneratedShortDate);
        }
    }
}
