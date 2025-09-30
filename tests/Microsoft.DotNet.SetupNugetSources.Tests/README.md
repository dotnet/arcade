# SetupNugetSources Tests

This test project provides comprehensive unit tests for the `SetupNugetSources.ps1` (Windows) and `SetupNugetSources.sh` (Mac/Linux) scripts located in `eng/common/`.

## Test Structure

The test project is organized into the following test classes:

### 1. NoChangeScenarioTests
Tests scenarios where the scripts should make no changes to the NuGet.config file:
- Basic configurations with only standard feeds (nuget.org)
- Configurations with non-.NET feeds
- Configurations with existing credentials but no special feeds

### 2. InternalFeedAdditionTests
Tests automatic addition of internal feeds based on public feed presence:
- Addition of `-internal` and `-internal-transport` feeds for each .NET version (5-10)
- Handling multiple .NET versions simultaneously
- Prevention of duplicate feed creation

### 3. FeedEnablingTests
Tests enabling of disabled internal feeds:
- Enabling `darc-int-*` feeds from `disabledPackageSources`
- Selective enabling (only darc-int feeds, not others)
- Preference for enabling existing disabled feeds over adding new ones

### 4. CredentialHandlingTests
Tests credential management functionality:
- Addition of credentials when password/token is provided
- Preservation of existing credentials when no password is provided
- Use of v2 endpoints when credentials are present, v3 otherwise
- Credential handling for both new and enabled feeds

### 5. BoundaryConditionTests
Tests edge cases and error conditions:
- Empty configurations
- Missing sections (`packageSources`, `disabledPackageSources`)
- Malformed XML handling
- Special characters in feed names
- Very long feed names
- Non-existent config files

## Cross-Platform Testing

The tests use parameterized tests with a `ScriptType` enum to abstract away platform-specific implementation details:
- `[Theory]` tests with `[MemberData(nameof(GetSupportedScriptTypes))]` run the same test logic for both PowerShell and shell scripts
- On Windows, both PowerShell and shell scripts are tested
- On Unix platforms, only shell scripts are tested
- The `ScriptRunner` class provides a unified interface via `RunScript(ScriptType, ...)` method

This approach ensures both script implementations are tested for the same behavior while eliminating duplicate test code.



## Helper Classes

### TestNuGetConfigFactory
Provides factory methods for creating various test NuGet.config scenarios:
- Basic configurations
- Configurations with .NET feeds
- Configurations with disabled sources
- Configurations with existing credentials
- Multi-version configurations

### ScriptRunner
Handles cross-platform execution of both PowerShell and shell scripts:
- PowerShell execution on Windows
- Bash execution on Unix platforms
- Proper argument passing and credential handling
- Process output capture and error reporting

### NuGetConfigAssertions
Provides fluent assertion methods for validating NuGet.config modifications:
- Semantic XML comparison (ignoring whitespace)
- Package source presence/absence verification
- Credential verification
- Disabled source status checking
- XML normalization for consistent comparisons

## Test Coverage

The tests cover all major variations specified in the requirements:

✅ **No-change scenarios**: Files with no "special" feeds have no non-whitespace changes
✅ **Internal feed addition**: Automatic addition based on public feed presence  
✅ **Feed enabling**: Enable existing disabled internal feeds
✅ **Credential handling**: Add credentials when provided, preserve when not
✅ **Boundary conditions**: Missing sections, malformed XML, edge cases
✅ **Platform-specific testing**: Tests run on appropriate platforms via CI

## Running the Tests

The tests use xUnit and can be run using standard .NET test commands:

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~NoChangeScenarioTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

## Test Dependencies

- **xUnit**: Test framework
- **FluentAssertions**: Fluent assertion library
- **Microsoft.DotNet.XUnitExtensions**: Platform-specific test conditions
- **Microsoft.Arcade.Test.Common**: Common test utilities

## Notes

- Tests automatically handle platform-specific execution (PowerShell on Windows, Bash on Unix)
- Platform-specific tests are run via CI across multiple platforms to ensure both script implementations work correctly
- Test files are automatically cleaned up after each test run
- All test scenarios use temporary directories to avoid interference
- Tests include both positive and negative scenarios to ensure robustness
