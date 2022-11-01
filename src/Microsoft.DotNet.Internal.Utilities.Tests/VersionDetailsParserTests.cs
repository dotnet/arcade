// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.Internal.Utilities.Tests
{
    public class VersionDetailsParserTests
    {
        [Fact]
        public void AreDependencyMetadataParsedTest()
        {
            const string VersionDetailsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Dependencies>
              <ProductDependencies>
                <Dependency Name="NETStandard.Library.Ref" Version="2.1.0" Pinned="true">
                  <Uri>https://github.com/dotnet/core-setup</Uri>
                  <Sha>7d57652f33493fa022125b7f63aad0d70c52d810</Sha>
                </Dependency>
                <Dependency Name="NuGet.Build.Tasks" Version="6.4.0-preview.1.51" CoherentParentDependency="Microsoft.NET.Sdk">
                  <Uri>https://github.com/nuget/nuget.client</Uri>
                  <Sha>745617ea6fc239737c80abb424e13faca4249bf1</Sha>
                  <SourceBuildTarball RepoName="nuget-client" />
                </Dependency>
              </ProductDependencies>
              <ToolsetDependencies>
                <Dependency Name="Microsoft.DotNet.Arcade.Sdk" Version="7.0.0-beta.22426.1">
                  <Uri>https://github.com/dotnet/arcade</Uri>
                  <Sha>692746db3f08766bc29e91e826ff15e5e8a82b44</Sha>
                  <SourceBuild RepoName="arcade" ManagedOnly="true" TarballOnly="true" />
                </Dependency>
              </ToolsetDependencies>
            </Dependencies>
            """;

            var parser = new VersionDetailsParser();
            var dependencies = parser.ParseVersionDetailsXml(VersionDetailsXml);

            dependencies.Should().HaveCount(3);
            dependencies.Should().Contain(d => d.Name == "NETStandard.Library.Ref"
                && d.Version == "2.1.0"
                && d.RepoUri == "https://github.com/dotnet/core-setup"
                && d.Commit == "7d57652f33493fa022125b7f63aad0d70c52d810"
                && d.Pinned
                && d.CoherentParentDependencyName == null
                && d.SourceBuild == null
                && d.Type == DependencyType.Product);

            dependencies.Should().Contain(d => d.Name == "NuGet.Build.Tasks"
                && d.Version == "6.4.0-preview.1.51"
                && d.RepoUri == "https://github.com/nuget/nuget.client"
                && d.Commit == "745617ea6fc239737c80abb424e13faca4249bf1"
                && !d.Pinned
                && d.CoherentParentDependencyName == "Microsoft.NET.Sdk"
                && d.SourceBuild != null
                && d.SourceBuild.RepoName == "nuget-client"
                && !d.SourceBuild.ManagedOnly
                && d.Type == DependencyType.Product);

            dependencies.Should().Contain(d => d.Name == "Microsoft.DotNet.Arcade.Sdk"
                && d.Version == "7.0.0-beta.22426.1"
                && d.RepoUri == "https://github.com/dotnet/arcade"
                && d.Commit == "692746db3f08766bc29e91e826ff15e5e8a82b44"
                && !d.Pinned
                && d.CoherentParentDependencyName == null
                && d.SourceBuild != null
                && d.SourceBuild.RepoName == "arcade"
                && d.SourceBuild.ManagedOnly
                && d.SourceBuild.TarballOnly
                && d.Type == DependencyType.Toolset);
        }

        [Fact]
        public void EmptyXmlIsHandledTest()
        {
            const string VersionDetailsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Dependencies></Dependencies>
            """;

            var parser = new VersionDetailsParser();
            var dependencies = parser.ParseVersionDetailsXml(VersionDetailsXml);
            dependencies.Should().BeEmpty();
        }

        [Fact]
        public void UnknownCategoryIsRecognizedTest()
        {
            const string VersionDetailsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Dependencies>
              <Something>
                <Dependency Name="Microsoft.DotNet.Arcade.Sdk" Version="7.0.0-beta.22426.1">
                  <Uri>https://github.com/dotnet/arcade</Uri>
                  <Sha>692746db3f08766bc29e91e826ff15e5e8a82b44</Sha>
                  <SourceBuild RepoName="arcade" ManagedOnly="true" TarballOnly="true" />
                </Dependency>
              </Something>
            </Dependencies>
            """;

            var parser = new VersionDetailsParser();
            var action = () => parser.ParseVersionDetailsXml(VersionDetailsXml);
            action.Should().Throw<ParserException>().WithMessage("Unknown dependency type*Something*");
        }

        [Fact]
        public void InvalidBooleanIsRecognizedTest()
        {
            const string VersionDetailsXml =
            """
            <?xml version="1.0" encoding="utf-8"?>
            <Dependencies>
              <ProductDependencies>
                <Dependency Name="Microsoft.DotNet.Arcade.Sdk" Version="7.0.0-beta.22426.1">
                  <Uri>https://github.com/dotnet/arcade</Uri>
                  <Sha>692746db3f08766bc29e91e826ff15e5e8a82b44</Sha>
                  <SourceBuild RepoName="arcade" ManagedOnly="foobar" />
                </Dependency>
              </ProductDependencies>
            </Dependencies>
            """;

            var parser = new VersionDetailsParser();
            var action = () => parser.ParseVersionDetailsXml(VersionDetailsXml);
            action.Should().Throw<ParserException>().WithMessage("*is not a valid boolean*");
        }
    }
}
