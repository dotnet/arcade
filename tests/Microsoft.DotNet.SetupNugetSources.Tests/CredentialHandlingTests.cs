using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public class CredentialHandlingTests
    {
        private readonly ScriptRunner _scriptRunner;
        private readonly string _testOutputDirectory;

        public CredentialHandlingTests()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "SetupNugetSourcesTests", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputDirectory);
            _scriptRunner = new ScriptRunner(_testOutputDirectory);
        }



        [Fact]
        public async Task ConfigWithCredentialProvided_AddsCredentialsForInternalFeeds()
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
            var testCredential = "test-token-12345";
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath, testCredential);

            // Assert
            result.exitCode.Should().Be(0, $"{scriptType} script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should add internal feeds
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport");
            
            // Should add credentials for internal feeds
            modifiedConfig.ShouldContainCredentials("dotnet6-internal", "dn-bot", "should add credentials for internal feed");
            modifiedConfig.ShouldContainCredentials("dotnet6-internal-transport", "dn-bot", "should add credentials for transport feed");
            
            // Should use v2 endpoints when credentials are provided
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v2",
                "should use v2 endpoint when credentials provided");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal-transport/nuget/v2",
                "should use v2 endpoint when credentials provided");
        }

        [Fact]
        public async Task ConfigWithNoCredential_DoesNotAddCredentials()
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

            // Act - No credential provided
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"{scriptType} script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should add internal feeds
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal");
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal-transport");
            
            // Should NOT add credentials
            modifiedConfig.ShouldNotContainCredentials("dotnet6-internal", "should not add credentials without credential");
            modifiedConfig.ShouldNotContainCredentials("dotnet6-internal-transport", "should not add credentials without credential");
            
            // Should use v3 endpoints when no credentials are provided
            modifiedConfig.ShouldContainPackageSource("dotnet6-internal", 
                "https://pkgs.dev.azure.com/dnceng/internal/_packaging/dotnet6-internal/nuget/v3/index.json",
                "should use v3 endpoint when no credentials provided");
        }

        [Fact]
        public async Task ConfigWithExistingCredentials_PreservesAndAddsNew()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
    <add key=""existing-private"" value=""https://example.com/nuget"" />
  </packageSources>
  <packageSourceCredentials>
    <existing-private>
      <add key=""Username"" value=""existing-user"" />
      <add key=""ClearTextPassword"" value=""existing-password"" />
    </existing-private>
  </packageSourceCredentials>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var testCredential = "new-token-12345";
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath, testCredential);

            // Assert
            result.exitCode.Should().Be(0, $"{scriptType} script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should preserve existing credentials
            modifiedConfig.ShouldContainCredentials("existing-private", "existing-user", "should preserve existing credentials");
            
            // Should add new credentials for internal feeds
            modifiedConfig.ShouldContainCredentials("dotnet6-internal", "dn-bot", "should add credentials for new internal feed");
            modifiedConfig.ShouldContainCredentials("dotnet6-internal-transport", "dn-bot", "should add credentials for new transport feed");
        }

        [Fact]
        public async Task ConfigWithDarcIntFeeds_AddsCredentialsForEnabledFeeds()
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
  </disabledPackageSources>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var testCredential = "test-token-12345";
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath, testCredential);

            // Assert
            result.exitCode.Should().Be(0, $"{scriptType} script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should enable the darc-int feed
            modifiedConfig.ShouldNotBeDisabled("darc-int-dotnet-roslyn-12345", "darc-int feed should be enabled");
            
            // Should add credentials for enabled darc-int feed
            modifiedConfig.ShouldContainCredentials("darc-int-dotnet-roslyn-12345", "dn-bot", "should add credentials for enabled darc-int feed");
            
            // Should add credentials for new internal feeds
            modifiedConfig.ShouldContainCredentials("dotnet6-internal", "dn-bot", "should add credentials for internal feed");
            modifiedConfig.ShouldContainCredentials("dotnet6-internal-transport", "dn-bot", "should add credentials for transport feed");
        }

        [Fact]
        public async Task ConfigWithNoCredentialButExistingCredentials_DoesNotRemoveExistingCredentials()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""dotnet6"" value=""https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet6/nuget/v3/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <dotnet6-internal>
      <add key=""Username"" value=""dn-bot"" />
      <add key=""ClearTextPassword"" value=""existing-token"" />
    </dotnet6-internal>
  </packageSourceCredentials>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act - No credential provided
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"{scriptType} script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            
            // Should preserve existing credentials
            modifiedConfig.ShouldContainCredentials("dotnet6-internal", "dn-bot", "should preserve existing credentials");
            
            // Should not add credentials for new feeds without credential
            modifiedConfig.ShouldNotContainCredentials("dotnet6-internal-transport", "should not add credentials without credential");
        }
    }
}
