using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.XUnitExtensions;
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
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
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

        [ConditionalFact(typeof(WindowsPlatformCondition))]
        public async Task PowerShell_ConfigWithMixedDisabledFeeds_OnlyEnablesDarcIntFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""some-other-feed"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
    <add key=""another-disabled-feed"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);

            // Act
            var result = await _scriptRunner.RunPowerShellScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Darc-int feeds should be enabled
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "darc-int feed should be enabled");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-runtime-67890", "darc-int feed should be enabled");
            
            // Non-darc-int feeds should remain disabled
            modifiedConfig.ShouldBeDisabled("some-other-feed", "non-darc-int feed should remain disabled");
            modifiedConfig.ShouldBeDisabled("another-disabled-feed", "non-darc-int feed should remain disabled");
        }

        [ConditionalFact(typeof(UnixPlatformCondition))]
        public async Task Shell_ConfigWithMixedDisabledFeeds_OnlyEnablesDarcIntFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""some-other-feed"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-67890"" value=""true"" />
    <add key=""another-disabled-feed"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);

            // Act
            var result = await _scriptRunner.RunShellScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Darc-int feeds should be enabled
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "darc-int feed should be enabled");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-runtime-67890", "darc-int feed should be enabled");
            
            // Non-darc-int feeds should remain disabled
            modifiedConfig.ShouldBeDisabled("some-other-feed", "non-darc-int feed should remain disabled");
            modifiedConfig.ShouldBeDisabled("another-disabled-feed", "non-darc-int feed should remain disabled");
        }

        [ConditionalFact(typeof(WindowsPlatformCondition))]
        public async Task PowerShell_ConfigWithDisabledInternalFeed_EnablesExistingInsteadOfAdding()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""dotnet6-internal"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);

            // Act
            var result = await _scriptRunner.RunPowerShellScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // The dotnet6-internal feed should be enabled (removed from disabled sources)
            modifiedConfig.ShouldNotBeDisabled("dotnet6-internal", "internal feed should be enabled");
            
            // Should still add the transport feed
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add transport feed");
            
            // Should have 3 package sources (original 2 + transport, not duplicating the enabled one)
            modifiedConfig.GetPackageSourceCount().Should().Be(3, "should not duplicate enabled feeds");
        }

        [ConditionalFact(typeof(UnixPlatformCondition))]
        public async Task Shell_ConfigWithDisabledInternalFeed_EnablesExistingInsteadOfAdding()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""dotnet6-internal"" value=""true"" />
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);

            // Act
            var result = await _scriptRunner.RunShellScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // The dotnet6-internal feed should be enabled (removed from disabled sources)
            modifiedConfig.ShouldNotBeDisabled("dotnet6-internal", "internal feed should be enabled");
            
            // Should still add the transport feed
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add transport feed");
            
            // Should have 3 package sources (original 2 + transport, not duplicating the enabled one)
            modifiedConfig.GetPackageSourceCount().Should().Be(3, "should not duplicate enabled feeds");
        }

        [ConditionalFact(typeof(WindowsPlatformCondition))]
        public async Task PowerShell_ConfigWithNoDisabledSources_StillAddsInternalFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);

            // Act
            var result = await _scriptRunner.RunPowerShellScript(configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
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
