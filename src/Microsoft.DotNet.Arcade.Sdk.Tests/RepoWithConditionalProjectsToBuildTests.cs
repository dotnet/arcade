// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    [Collection(TestProjectCollection.Name)]
    public class RepoWithConditionalProjectsToBuildTests
    {
        private readonly ITestOutputHelper _output;
        private readonly TestProjectFixture _fixture;

        public RepoWithConditionalProjectsToBuildTests(ITestOutputHelper output, TestProjectFixture fixture)
        {
            _output = output;
            _fixture = fixture;
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 2)]
        public void RepoProducesPackages(bool buildAdditionalProject, int expectedPackages)
        {
            var app = _fixture.CreateTestApp("RepoWithConditionalProjectsToBuild");
            var packArg = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "-pack"
                : "--pack";
            var exitCode = app.ExecuteBuild(_output,
                packArg,
                $"/p:ShouldBuildMaybe={buildAdditionalProject}",
                // these properties are required for projects that are not in a git repo
                "/p:EnableSourceLink=false",
                "/p:EnableSourceControlManagerQueries=false");
            Assert.Equal(0, exitCode);
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
