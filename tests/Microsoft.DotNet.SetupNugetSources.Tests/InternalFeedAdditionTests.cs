using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public class InternalFeedAdditionTests
    {
        private readonly ScriptRunner _scriptRunner;
        private readonly string _testOutputDirectory;

        public InternalFeedAdditionTests()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "SetupNugetSourcesTests", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputDirectory);
            _scriptRunner = new ScriptRunner(_testOutputDirectory);
        }

        [Fact]
        public async Task ConfigWithDotNet6_AddsInternalFeeds()
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
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json",
                "should add dotnet6-internal feed");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add dotnet6-internal-transport feed");
            
            // Original sources should still be present
            modifiedConfig.ShouldContainPackageSource("nuget.org", 
                "https://api.nuget.org/v3/index.json",
                "should preserve original nuget.org feed");
            modifiedConfig.ShouldContainPackageSource("dotnet6", 
                "https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json",
                "should preserve original dotnet6 feed");
        }

        [Theory]
        [InlineData("dotnet5")]
        [InlineData("dotnet6")]
        [InlineData("dotnet7")]
        [InlineData("dotnet8")]
        [InlineData("dotnet9")]
        [InlineData("dotnet10")]
        public async Task ConfigWithSpecificDotNetVersion_AddsCorrespondingInternalFeeds(string dotnetVersion)
        {
            // Arrange
            var originalConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""{dotnetVersion}"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/{dotnetVersion}/nuget/v3/index.json"" />
  </packageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            modifiedConfig.ShouldContainPackageSource($"{dotnetVersion}-internal", 
                $"https://pkgs.dev.azure.com/dnceng/internal/_packaging/{dotnetVersion}-internal/nuget/v3/index.json",
                $"should add {dotnetVersion}-internal feed");
            modifiedConfig.ShouldContainPackageSource($"{dotnetVersion}-internal-transport", 
                $"https://pkgs.dev.azure.com/dnceng/internal/_packaging/{dotnetVersion}-internal-transport/nuget/v3/index.json",
                $"should add {dotnetVersion}-internal-transport feed");
        }

        [Fact]
        public async Task ConfigWithMultipleDotNetVersions_AddsAllInternalFeeds()
        {
            // Arrange
            var originalConfig = TestNuGetConfigFactory.CreateConfigWithMultipleDotNetVersions();
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should add internal feeds for all versions
            var versions = new[] { "dotnet5", "dotnet6", "dotnet7", "dotnet8", "dotnet9", "dotnet10" };
            foreach (var version in versions)
            {
                modifiedConfig.ShouldContainPackageSource($"{version}-internal", 
                    $"https://pkgs.dev.azure.com/dnceng/internal/_packaging/{version}-internal/nuget/v3/index.json",
                    $"should add {version}-internal feed");
                modifiedConfig.ShouldContainPackageSource($"{version}-internal-transport", 
                    $"https://pkgs.dev.azure.com/dnceng/internal/_packaging/{version}-internal-transport/nuget/v3/index.json",
                    $"should add {version}-internal-transport feed");
            }
            
            // Original count (7 sources) + 12 internal sources = 19 total
            modifiedConfig.GetPackageSourceCount().Should().Be(19, "should have all original sources plus internal feeds");
        }

        [Fact]
        public async Task ConfigWithExistingInternalFeed_DoesNotDuplicate()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <add key=""dotnet6-internal"" value=""https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json"" />
  </packageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should still contain the dotnet6-internal feed (only once)
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json",
                "existing internal feed should be preserved");
            
            // Should add the missing transport feed
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v3/index.json",
                "should add missing transport feed");
            
            // Should have 4 total sources (3 original + 1 added transport)
            modifiedConfig.GetPackageSourceCount().Should().Be(4, "should not duplicate existing sources");
        }
    }
}
