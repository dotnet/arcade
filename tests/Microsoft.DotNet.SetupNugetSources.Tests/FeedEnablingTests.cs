// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public class FeedEnablingTests
    {
        private readonly ScriptRunner _scriptRunner;
        private readonly string _testOutputDirectory;

        public FeedEnablingTests()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "SetupNugetSourcesTests", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputDirectory);
            _scriptRunner = new ScriptRunner(_testOutputDirectory);
        }

        [Fact]
        public async Task ConfigWithDisabledDarcIntFeeds_EnablesFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <add key=""darc-int-dotnet-roslyn-12345"" value=""https://pkgs.dev.azure.com/dnceng/internal/_packaging/darc-int-dotnet-roslyn-12345/nuget/v3/index.json"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""https://pkgs.dev.azure.com/dnceng/internal/_packaging/darc-int-dotnet-runtime-67890/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await Task.Run(() => File.WriteAllText(configPath, originalConfig));
            // Act
            var result = await _scriptRunner.RunScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, "Script should succeed, but got error: {result.error}");
            var modifiedConfig = await Task.Run(() => File.ReadAllText(configPath));
            
            // Darc-int feeds should no longer be disabled
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "darc-int feed should be enabled");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-runtime-67890", "darc-int feed should be enabled");
            
            // Should also add internal feeds for dotnet6
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json",
                "should add dotnet6-internal feed");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add dotnet6-internal-transport feed");
        }

        [Fact]
        public async Task ConfigWithMixedDisabledFeeds_OnlyEnablesDarcIntFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
    <add key=""darc-int-dotnet-roslyn-12345"" value=""https://pkgs.dev.azure.com/dnceng/internal/_packaging/darc-int-dotnet-roslyn-12345/nuget/v3/index.json"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""https://pkgs.dev.azure.com/dnceng/internal/_packaging/darc-int-dotnet-runtime-67890/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""some-other-feed"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
    <add key=""another-disabled-feed"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await Task.Run(() => File.WriteAllText(configPath, originalConfig));

            // Act
            var result = await _scriptRunner.RunScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, "Script should succeed, but got error: {result.error}");
            var modifiedConfig = await Task.Run(() => File.ReadAllText(configPath));
            
            // Darc-int feeds should be enabled
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "darc-int feed should be enabled");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-runtime-67890", "darc-int feed should be enabled");
            
            // Non-darc-int feeds should remain disabled
            modifiedConfig.ShouldBeDisabled("some-other-feed", "non-darc-int feed should remain disabled");
            modifiedConfig.ShouldBeDisabled("another-disabled-feed", "non-darc-int feed should remain disabled");
        }

        [Fact]
        public async Task ConfigWithDisabledInternalFeed_EnablesExistingInsteadOfAdding()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <add key=""dotnet6-internal"" value=""https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""dotnet6-internal"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await Task.Run(() => File.WriteAllText(configPath, originalConfig));

            // Act
            var result = await _scriptRunner.RunScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, "Script should succeed, but got error: {result.error}");
            var modifiedConfig = await Task.Run(() => File.ReadAllText(configPath));
            
            // The dotnet6-internal feed should be enabled (removed from disabled sources)
            modifiedConfig.ShouldNotBeDisabled("dotnet6-internal", "internal feed should be enabled");
            
            // Should still add the transport feed
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add transport feed");
            
            // Should have 3 package sources (original 2 + transport, not duplicating the enabled one)
            modifiedConfig.GetPackageSourceCount().Should().Be(3, "should not duplicate enabled feeds");
        }

        [Fact]
        public async Task ConfigWithNoDisabledSources_StillAddsInternalFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""dotnet-public"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-public/nuget/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await Task.Run(() => File.WriteAllText(configPath, originalConfig));

            // Act
            var result = await _scriptRunner.RunScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, "Script should succeed, but got error: {result.error}");
            var modifiedConfig = await Task.Run(() => File.ReadAllText(configPath));
            
            // Should add internal feeds even without disabled sources section
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json",
                "should add dotnet6-internal feed");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add dotnet6-internal-transport feed");
        }
    }
}


