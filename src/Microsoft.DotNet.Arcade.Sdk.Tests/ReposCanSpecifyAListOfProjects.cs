using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class ReposCanSpecifyAListOfProjects 
    {
        private readonly ITestOutputHelper _output;
        private readonly TestProjectFixture _fixture;

        public ReposCanSpecifyAListOfProjects(ITestOutputHelper output, TestProjectFixture fixture)
        {
            _output = output;
            _fixture = fixture;
        }

        [Theory]
        [InlineData("/p:ShouldBuildMaybe=false", 1)]
        [InlineData("/p:ShouldBuildMaybe=true", 2)]
        public void RepoProducesPackages(string buildArgs, int expectedPackages)
        {
            var app = _fixture.CreateTestApp("RepoWithConditionalProjectsToBuild");

            Assert.Equal(0, app.ExecuteBuild(_output, "-pack", buildArgs, "/p:EnableSourceLink=false", "/p:EnableSourceControlManagerQueries=false"));
            var nupkgFiles = Directory.GetFiles(Path.Combine(app.WorkingDirectory, "artifacts", "packages", "Debug", "Shipping"), "*.nupkg");

            _output.WriteLine("Packages produced:");

            foreach(var file in nupkgFiles)
            {
                _output.WriteLine(file);
            }

            Assert.Equal(expectedPackages, nupkgFiles.Length);
        }
    }
}
