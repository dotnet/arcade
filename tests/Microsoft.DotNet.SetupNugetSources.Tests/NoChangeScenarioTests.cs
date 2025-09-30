using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.DotNet.XUnitExtensions;
using Xunit;

namespace Microsoft.DotNet.SetupNugetSources.Tests
{
    public class NoChangeScenarioTests
    {
        private readonly ScriptRunner _scriptRunner;
        private readonly string _testOutputDirectory;

        public NoChangeScenarioTests()
        {
            _testOutputDirectory = Path.Combine(Path.GetTempPath(), "SetupNugetSourcesTests", System.Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testOutputDirectory);
            _scriptRunner = new ScriptRunner(_testOutputDirectory);
        }

        public static TheoryData<ScriptType> GetSupportedScriptTypes()
        {
            var data = new TheoryData<ScriptType>();
            foreach (var scriptType in ScriptRunner.GetAllSupportedScriptTypes())
            {
                data.Add(scriptType);
            }
            return data;
        }

        [Theory]
        [MemberData(nameof(GetSupportedScriptTypes))]
        public async Task BasicConfig_NoChanges(ScriptType scriptType)
        {
            // Arrange
            var originalConfig = TestNuGetConfigFactory.CreateBasicConfig();
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"{scriptType} script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            modifiedConfig.ShouldBeSemanticallySame(originalConfig, "basic config with no special feeds should not be modified");
        }

        [Fact]
        public async Task ConfigWithNonDotNetFeeds_NoChanges()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""private-feed"" value=""https://example.com/nuget/v3/index.json"" />
    <add key=""company-internal"" value=""https://company.example.com/nuget"" />
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
            modifiedConfig.ShouldBeSemanticallySame(originalConfig, "config with non-dotnet feeds should not be modified");
        }

        [Fact]
        public async Task ConfigWithCredentialsButNoSpecialFeeds_NoChanges()
        {
            // Arrange
            var originalConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" />
    <add key=""private-feed"" value=""https://example.com/nuget/v3/index.json"" />
  </packageSources>
  <packageSourceCredentials>
    <private-feed>
      <add key=""Username"" value=""user"" />
      <add key=""ClearTextPassword"" value=""existing-password"" />
    </private-feed>
  </packageSourceCredentials>
</configuration>";
            var configPath = Path.Combine(_testOutputDirectory, "nuget.config");
            await File.WriteAllTextAsync(configPath, originalConfig);
            var scriptType = ScriptRunner.GetPlatformAppropriateScriptType();

            // Act
            var result = await _scriptRunner.RunScript(scriptType, configPath);

            // Assert
            result.exitCode.Should().Be(0, $"Script should succeed, but got error: {result.error}");
            var modifiedConfig = await File.ReadAllTextAsync(configPath);
            modifiedConfig.ShouldBeSemanticallySame(originalConfig, "config with credentials but no special feeds should not be modified");
        }
    }
}
