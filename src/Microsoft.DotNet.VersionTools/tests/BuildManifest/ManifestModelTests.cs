﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System.Xml.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.VersionTools.Tests.BuildManifest
{
    public class ManifestModelTests
    {
        private readonly ITestOutputHelper _output;

        public ManifestModelTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TestExampleBuildManifestRoundtrip()
        {
            XElement xml = XElement.Parse(ExampleBuildString);
            var model = BuildModel.Parse(xml);
            XElement modelXml = model.ToXml();

            Assert.True(
                XNode.DeepEquals(xml, modelXml),
                "Model failed to output the parsed XML.");
        }

        [Fact]
        public void TestExampleOrchestratedBuildManifestRoundtrip()
        {
            XElement xml = XElement.Parse(ExampleOrchestratedBuildString);
            var model = OrchestratedBuildModel.Parse(xml);
            XElement modelXml = model.ToXml();

            Assert.True(
                XNode.DeepEquals(xml, modelXml),
                "Model failed to output the parsed XML.");
        }

        [Fact]
        public void TestExampleCustomBuildIdentityRoundtrip()
        {
            XElement xml = XElement.Parse(
                @"<Build Name=""Example"" BuildId=""123"" ProductVersion=""1.0.0-preview"" Branch=""master"" Commit=""abcdef"" BlankExtra="""" Extra=""extra-foo"" />");
            var model = BuildModel.Parse(xml);
            XElement modelXml = model.ToXml();

            Assert.True(
                XNode.DeepEquals(xml, modelXml),
                "Model failed to output the parsed XML.");
        }

        [Fact]
        public void TestPackageOnlyBuildManifest()
        {
            var model = CreatePackageOnlyBuildManifestModel();
            XElement modelXml = model.ToXml();
            XElement xml = XElement.Parse(@"<Build Name=""SimpleBuildManifest"" BuildId=""123""><Package Id=""Foo"" Version=""1.2.3-example"" /></Build>");

            Assert.True(XNode.DeepEquals(xml, modelXml));
        }

        [Fact]
        public void TestMergeBuildManifests()
        {
            var orchestratedModel = new OrchestratedBuildModel(new BuildIdentity { Name = "Orchestrated", BuildId = "123" })
            {
                Endpoints = new List<EndpointModel>
                {
                    EndpointModel.CreateOrchestratedBlobFeed("http://example.org")
                }
            };

            orchestratedModel.AddParticipantBuild(CreatePackageOnlyBuildManifestModel());
            orchestratedModel.AddParticipantBuild(BuildModel.Parse(XElement.Parse(ExampleBuildString)));

            XElement modelXml = orchestratedModel.ToXml();
            XElement xml = XElement.Parse(@"
<OrchestratedBuild Name=""Orchestrated"" BuildId=""123"">
  <Endpoint Id=""Orchestrated"" Type=""BlobFeed"" Url=""http://example.org"">
    <Package Id=""Foo"" Version=""1.2.3-example"" />
    <Package Id=""runtime.rhel.6-x64.Microsoft.Private.CoreFx.NETCoreApp"" Version=""4.5.0-preview1-25929-04"" Category=""noship"" />
    <Package Id=""System.Memory"" Version=""4.5.0-preview1-25927-01"" />
    <Blob Id=""symbols/inner/blank-dir-nonshipping"" NonShipping=""false"" />
    <Blob Id=""symbols/runtime.rhel.6-x64.Microsoft.Private.CoreFx.NETCoreApp.4.5.0-preview1-25929-04.symbols.nupkg"" />
    <Blob Id=""symbols/System.ValueTuple.4.5.0-preview1-25929-04.symbols.nupkg"" NonShipping=""true"" />
  </Endpoint>
  <Build Name=""SimpleBuildManifest"" BuildId=""123"" />
  <Build Name=""corefx"" BuildId=""20171129-04"" Branch=""master"" Commit=""defb6d52047cc3d6b5f5d0853b0afdb1512dfbf4"" />
</OrchestratedBuild>");

            Assert.True(XNode.DeepEquals(xml, modelXml));
        }

        private BuildModel CreatePackageOnlyBuildManifestModel()
        {
            return new BuildModel(new BuildIdentity { Name = "SimpleBuildManifest", BuildId = "123" })
            {
                Artifacts = new ArtifactSet
                {
                    Packages = new List<PackageArtifactModel>
                    {
                        new PackageArtifactModel
                        {
                            Id = "Foo",
                            Version = "1.2.3-example"
                        }
                    }
                }
            };
        }

        private const string ExampleBuildString = @"
<Build
  Name=""corefx""
  BuildId=""20171129-04""
  Branch=""master""
  Commit=""defb6d52047cc3d6b5f5d0853b0afdb1512dfbf4"">

  <Package Id=""runtime.rhel.6-x64.Microsoft.Private.CoreFx.NETCoreApp"" Version=""4.5.0-preview1-25929-04"" Category=""noship"" />
  <Package Id=""System.Memory"" Version=""4.5.0-preview1-25927-01"" />

  <Blob Id=""symbols/inner/blank-dir-nonshipping"" NonShipping=""false"" />
  <Blob Id=""symbols/runtime.rhel.6-x64.Microsoft.Private.CoreFx.NETCoreApp.4.5.0-preview1-25929-04.symbols.nupkg"" />
  <Blob Id=""symbols/System.ValueTuple.4.5.0-preview1-25929-04.symbols.nupkg"" NonShipping=""true"" />

</Build>";

        private const string ExampleOrchestratedBuildString = @"
<OrchestratedBuild
  Name=""core-setup""
  BuildId=""20171129-02""
  Branch=""master"">

  <Endpoint
    Id=""Orchestrated""
    Type=""BlobFeed""
    Url=""https://dotnetfeed.blob.core.windows.net/orchestrated-aspnet/20171129-02/index.json"">

    <Package Id=""Microsoft.NETCore.App"" Version=""2.1.0-preview1-26001-02"" />
    <Package Id=""Microsoft.NETCore.UniversalWindowsPlatform"" Version=""6.1.0-preview1-25927-01"" NonShipping=""true"" />
    <Package Id=""runtime.rhel.6-x64.Microsoft.Private.CoreFx.NETCoreApp"" Version=""4.5.0-preview1-25929-04"" NonShipping=""true"" />
    <Package Id=""System.Memory"" Version=""4.5.0-preview1-25927-01"" />

    <Blob Id=""orchestration-metadata/manifests/core-setup.xml"" />
    <Blob Id=""orchestration-metadata/manifests/corefx.xml"" />
    <Blob Id=""orchestration-metadata/PackageVersions.props"" />
    <Blob Id=""Runtime/2.1.0-preview1-25929-04/dotnet-runtime-2.1.0-preview1-25929-04-win-x64.msi"" ShipInstaller=""dotnetcli"" />
    <Blob Id=""Runtime/2.1.0-preview1-25929-04/dotnet-runtime-2.1.0-preview1-25929-04-win-x64.msi.sha512"" ShipInstaller=""dotnetclichecksums"" />
    <Blob Id=""symbols/Microsoft.DotNet.PlatformAbstractions.2.1.0-preview1-25929-04.symbols.nupkg"" />
    <Blob Id=""symbols/runtime.rhel.6-x64.Microsoft.Private.CoreFx.NETCoreApp.4.5.0-preview1-25929-04.symbols.nupkg"" />
    <Blob Id=""symbols/System.ValueTuple.4.5.0-preview1-25929-04.symbols.nupkg"" />

  </Endpoint>

  <Build
    Name=""corefx""
    BuildId=""20171129-04""
    Branch=""master""
    Commit=""defb6d52047cc3d6b5f5d0853b0afdb1512dfbf4"" />

  <Build
    Name=""core-setup""
    BuildId=""20171129-04""
    Branch=""master""
    Commit=""152dbe8a4b4e30eee26208ff6a850e9aa73c07f8"" />

</OrchestratedBuild>
";
    }
}
