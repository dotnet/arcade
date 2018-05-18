// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.TestsUtil;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Versioning.Tests
{
    public class GenerateVersionTests
    {
        private readonly ITestOutputHelper _output;

        public GenerateVersionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicTestDefaults()
        {
            clearStatics();

            DateTime BaselineDate = new DateTime(1996, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            DateTime BuildDate = DateTime.UtcNow;

            int months = (BuildDate.Year - BaselineDate.Year) * 12 + BuildDate.Month - BaselineDate.Month;

            string expectedDate = string.Format("{0}{1}", months.ToString("D"), BuildDate.Day.ToString("D2"));

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine()
            };

            Assert.True(task.Execute());
            Assert.Equal("00", task.GeneratedRevision);
            //Assert.Equal(expectedDate, task.GeneratedShortDate);
            //Assert.Equal("", task.GeneratedShortSha);
        }

        [Fact]
        public void BasicTestAllStatics()
        {
            clearStatics();

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine()
            };

            GenerateVersion.SHA = "ddccbbaa";
            GenerateVersion.Revision = "09";
            GenerateVersion.Date = "26432";

            Assert.True(task.Execute());
            Assert.Equal("09", task.GeneratedRevision);
            Assert.Equal("26432", task.GeneratedShortDate);
            Assert.Equal("ddccbbaa", task.GeneratedShortSha);
        }

        [Fact]
        public void TestWithOfficialBuildId()
        {
            clearStatics();

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine(),
                OfficialBuildId = "20180102-03"
            };

            Assert.True(task.Execute());
            Assert.Equal("03", task.GeneratedRevision);
            Assert.Equal("26102", task.GeneratedShortDate);
            //Assert.Equal("", task.GeneratedShortSha);
        }

        [Fact]
        public void BasicTestOfficialBuildIdAndAllStatics()
        {
            clearStatics();

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine(),
                OfficialBuildId = "20180102-03"
            };

            GenerateVersion.SHA = "ddccbbaa";
            GenerateVersion.Revision = "09";
            GenerateVersion.Date = "26432";

            Assert.True(task.Execute());
            Assert.Equal("09", task.GeneratedRevision);
            Assert.Equal("26432", task.GeneratedShortDate);
            Assert.Equal("ddccbbaa", task.GeneratedShortSha);
        }

        [Fact]
        public void TestWithCommitSHA()
        {
            clearStatics();

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine(),
                OfficialBuildId = "20180102-03",
            };

            GenerateVersion.SHA = "aabbccee";

            Assert.True(task.Execute());
            Assert.Equal("03", task.GeneratedRevision);
            Assert.Equal("26102", task.GeneratedShortDate);
            Assert.Equal("aabbccee", task.GeneratedShortSha);
        }

        [Fact]
        public void TestWithCommitDate()
        {
            clearStatics();

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine(),
                OfficialBuildId = "20180102-03",
            };

            GenerateVersion.Date = "54321";

            Assert.True(task.Execute());
            Assert.Equal("03", task.GeneratedRevision);
            Assert.Equal("54321", task.GeneratedShortDate);
            //Assert.Equal("aabbccee", task.GeneratedShortSha);
        }

        [Fact]
        public void TestWithRevision()
        {
            clearStatics();

            var task = new GenerateVersion
            {
                BuildEngine = new MockEngine(),
                OfficialBuildId = "20180102-03",
            };

            GenerateVersion.Revision = "07";

            Assert.True(task.Execute());
            Assert.Equal("07", task.GeneratedRevision);
            Assert.Equal("26102", task.GeneratedShortDate);
            //Assert.Equal("aabbccee", task.GeneratedShortSha);
        }

        private void clearStatics()
        {
            GenerateVersion.SHA = String.Empty;
            GenerateVersion.Revision = String.Empty;
            GenerateVersion.Date = String.Empty;
        }
    }
}

