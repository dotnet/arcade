// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Build.Tasks.Versioning.Tests
{
    public class GenerateVersionComponentsTests
    {
        private readonly ITestOutputHelper _output;

        public GenerateVersionComponentsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void FromOfficialBuildId()
        {
            var task = new GenerateVersionComponents
            {
                OfficialBuildId = "19990101-33",
                BuildEngine = new TestsUtil.MockEngine(_output),
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal("33", task.GeneratedRevision);
            Assert.Equal("3301", task.GeneratedShortDate);
        }

        [Fact]
        public void FromOfficialBuildIdWithPadding()
        {
            var task = new GenerateVersionComponents
            {
                OfficialBuildId = "19990101-33",
                SemanticVersioningV1 = true,
                BuildEngine = new TestsUtil.MockEngine(_output),
            };

            Assert.True(task.Execute(), "Task should pass");
            Assert.Equal("33", task.GeneratedRevision);
            Assert.Equal("03301", task.GeneratedShortDate);
        }
    }
}

