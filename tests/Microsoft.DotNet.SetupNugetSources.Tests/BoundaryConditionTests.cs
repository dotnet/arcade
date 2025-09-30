using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public class BoundaryConditionTests
    {
        private readonly ScriptRunner _scriptRunner;
        private readonly string _testOutputDirectory;

        public BoundaryConditionTests()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "SetupNugetSourcesTests", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputDirectory);
            _scriptRunner = new ScriptRunner(_testOutputDirectory);
        }

        [Fact]
        public async Task EmptyConfiguration_CreatesPackageSourcesSection()
        {
            // Arrange
            var originalConfig = TestNuGetConfigFactory.CreateEmptyConfig();
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should create packageSources section but not add any sources since no dotnet feeds exist
            modifiedConfig.Should().Contain("<packageSources>", "should create packageSources section");
            modifiedConfig.Should().Contain("</packageSources>", "should close packageSources section");
        }

        [Fact]
        public async Task ConfigWithoutPackageSourcesSection_AddsSection()
        {
            // Arrange
            var originalConfig = TestNuGetConfigFactory.CreateConfigWithoutPackageSources();
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should create packageSources section and enable disabled darc-int feeds
            modifiedConfig.Should().Contain("<packageSources>", "should create packageSources section");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-runtime-67890", "should enable darc-int feed");
        }

        [Fact]
        public async Task ConfigWithMissingDisabledPackageSourcesSection_StillAddsInternalFeeds()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <!-- No disabledPackageSources section -->
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should still add internal feeds
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport");
        }

        [Fact]
        public async Task NonExistentConfigFile_ReturnsError()
        {
            // Arrange
            var nonExistentPath = Path.Combine(_testOutputDirectory, "nonexistent.config");
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, nonExistentPath);

            // Assert
            result.exitCode.Should().Be(1, "should return error code for nonexistent file");
            result.error.Should().Contain("Couldn't find the NuGet config file", "should report missing file error");
        }

        [Fact]
        public async Task MalformedXmlConfig_HandlesGracefully()
        {
            // Arrange
            var malformedConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <!-- Missing closing tag for packageSources -->
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, malformedConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            // The script should either succeed after fixing the XML or fail with an appropriate error
            if (result.exitCode != 0)
            {
                result.error.Should().Contain("XML", "should report XML parsing error if it fails");
            }
        }

        [Fact]
        public async Task ConfigWithOnlyDisabledSources_HandlesCorrectly()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
    <add key=""some-other-disabled"" value=""true"" />
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
            
            // Should enable darc-int feeds and create packageSources section
            modifiedConfig.Should().Contain("<packageSources>", "should create packageSources section");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "should enable darc-int feed");
            modifiedConfig.ShouldBeDisabled("some-other-disabled", "should leave non-darc-int feeds disabled");
        }

        [Fact]
        public async Task ConfigWithEmptyPackageSourcesSection_HandlesCorrectly()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <!-- Empty packageSources section -->
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345"" value=""true"" />
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
            
            // Should enable darc-int feeds but not add any dotnet internal feeds since no dotnet feeds exist
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "should enable darc-int feed");
            modifiedConfig.GetPackageSourceCount().Should().Be(0, "should not add dotnet internal feeds without dotnet public feeds");
        }

        [Fact]
        public async Task ConfigWithSpecialCharactersInFeedNames_HandlesCorrectly()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""darc-int-dotnet-roslyn-12345-with-special-chars!@#"" value=""true"" />
    <add key=""darc-int-dotnet-runtime-with-&amp;-entities"" value=""true"" />
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
            
            // Should handle special characters correctly
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345-with-special-chars!@#", "should handle special characters in feed names");
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-runtime-with-&-entities", "should handle XML entities in feed names");
            
            // Should still add internal feeds
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport");
        }

        [Fact]
        public async Task ConfigWithVeryLongFeedNames_HandlesCorrectly()
        {
            // Arrange
            var longFeedName = "darc-int-" + new string('a', 200);
            var originalConfig = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <disabledPackageSources>
    <add key=""{longFeedName}"" value=""true"" />
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
            
            // Should handle very long feed names
            modifiedConfig.ShouldNotBeDisabled(longFeedName, "should handle very long feed names");
        }
    }
}
