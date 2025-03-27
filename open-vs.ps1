. $PSScriptRoot\eng\common\tools.ps1

# This tells .NET Core to use the bootstrapped runtime
$env:DOTNET_ROOT=InitializeDotNetCli -install:$true -createSdkLocationFile:$true

# This tells MSBuild to load the SDK from the directory of the bootstrapped SDK
$env:DOTNET_MSBUILD_SDK_RESOLVER_CLI_DIR=$env:DOTNET_ROOT

# This tells .NET Core not to go looking for .NET Core in other places
$env:DOTNET_MULTILEVEL_LOOKUP=0;

# Put our local dotnet.exe on PATH first so Visual Studio knows which one to use
$env:PATH=($env:DOTNET_ROOT + ";" + $env:PATH);

# Disable .NET runtime signature validation errors which errors for local builds
$env:VSDebugger_ValidateDotnetDebugLibSignatures=0;

# Launch Visual Studio with the locally defined environment variables
& "$PSScriptRoot\Arcade.sln"
