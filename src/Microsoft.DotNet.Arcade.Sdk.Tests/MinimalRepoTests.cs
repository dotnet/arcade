using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class MinimalRepoTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestProjectFixture _fixture;

        public MinimalRepoTests(ITestOutputHelper output, TestProjectFixture fixture)
        {
            _output = output;
            _fixture = fixture;
        }

        [Fact]
        public void MinimalRepoBuildsWithoutErrors()
        {
            var app = _fixture.CreateTestApp("MinimalRepo");
            var exitCode = app.ExecuteBuild(_output, 
                // these properties are required for projects that are not in a git repo
                "/p:EnableSourceLink=false", 
                "/p:EnableSourceControlManagerQueries=false");
            Assert.Equal(0, exitCode);
        }
    }
}
