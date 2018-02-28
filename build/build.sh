#!/bin/bash

build=false
ci=false
configuration="Debug"
help=false
log=false
pack=false
prepareMachine=false
rebuild=false
restore=false
sign=false
solution=""
test=false
verbosity="minimal"
properties=""

while [[ $# > 0 ]]; do
  lowerI="$(echo $1 | awk '{print tolower($0)}')"
  case $lowerI in
    --build)
      build=true
      shift 1
      ;;
    --ci)
      ci=true
      shift 1
      ;;
    --configuration)
      configuration=$2
      shift 2
      ;;
    --help)
      echo "Common settings:"
      echo "  --configuration <value>  Build configuration Debug, Release"
      echo "  --verbosity <value>    Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
      echo "  --help           Print help and exit"
      echo ""
      echo "Actions:"
      echo "  --restore        Restore dependencies"
      echo "  --build          Build solution"
      echo "  --rebuild        Rebuild solution"
      echo "  --test           Run all unit tests in the solution"
      echo "  --sign           Sign build outputs"
      echo "  --pack           Package build outputs into NuGet packages and Willow components"
      echo ""
      echo "Advanced settings:"
      echo "  --solution <value>     Path to solution to build"
      echo "  --ci           Set when running on CI server"
      echo "  --log          Enable logging (by default on CI)"
      echo "  --prepareMachine     Prepare machine for CI run"
      echo ""
      echo "Command line arguments not listed above are passed through to MSBuild."
      exit 0
      ;;
    --log)
      log=true
      shift 1
      ;;
    --pack)
      pack=true
      shift 1
      ;;
    --prepareMachine)
      prepareMachine=true
      shift 1
      ;;
    --rebuild)
      rebuild=true
      shift 1
      ;;
    --restore)
      restore=true
      shift 1
      ;;
    --sign)
      sign=true
      shift 1
      ;;
    --solution)
      solution=$2
      shift 2
      ;;
    --test)
      test=true
      shift 1
      ;;
    --verbosity)
      verbosity=$2
      shift 2
      ;;
    *)
      properties="$properties $1"
      shift 1
      ;;
  esac
done

function CreateDirectory {
  if [ ! -d "$1" ]
  then
    mkdir -p "$1"
  fi
}

function readJson {  
  UNAMESTR=`uname`
  SED_EXTENDED='-r'
  if [[ "$UNAMESTR" == 'Darwin' ]]; then
    SED_EXTENDED='-E'
  fi; 
  JsonValue=`grep -m 1 "\"${2}\"" ${1} | sed ${SED_EXTENDED} 's/^ *//;s/.*: *"//;s/",?//'`
  if [ ! "$JsonValue" ]; then
    echo "Error: Cannot find \"${2}\" in ${1}" >&2;
    exit 1;
  fi; 
}

function InstallDotNetCli {
  readJson "$GlobalJsonFile" "version"
  DotNetCliVersion=$JsonValue
  DotNetInstallVerbosity=""

  if [ -z "$DOTNET_INSTALL_DIR" ]
  then
    export DOTNET_INSTALL_DIR="$RepoRoot/artifacts/.dotnet/$DotNetCliVersion"
  fi

  DotNetRoot=$DOTNET_INSTALL_DIR
  DotNetInstallScript="$DotNetRoot/dotnet-install.sh"

  if [ ! -a "$DotNetInstallScript" ]
  then
    CreateDirectory "$DotNetRoot"
    if command -v curl > /dev/null; then
      curl "https://dot.net/v1/dotnet-install.sh" -sSL --retry 10 --create-dirs -o "$DotNetInstallScript"
    else
      wget -q -O "$DotNetInstallScript" "https://dot.net/v1/dotnet-install.sh"
    fi
  fi

  if [[ "$(echo $verbosity | awk '{print tolower($0)}')" == "diagnostic" ]]
  then
    DotNetInstallVerbosity="--verbose"
  fi

  # Install a stage 0
  SdkInstallDir="$DotNetRoot/sdk/$DotNetCliVersion"

  if [ ! -d "$SdkInstallDir" ]
  then
    bash "$DotNetInstallScript" --version $DotNetCliVersion $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install stage0"
      return $LASTEXITCODE
    fi
  fi

  # Install 1.0 shared framework
  NetCoreApp10Version="1.0.5"
  NetCoreApp10Dir="$DotNetRoot/shared/Microsoft.NETCore.App/$NetCoreApp10Version"

  if [ ! -d "$NetCoreApp10Dir" ]
  then
    echo "bash $DotNetInstallScript --channel Preview --version $NetCoreApp10Version --shared-runtime $DotNetInstallVerbosity"
    bash "$DotNetInstallScript" --channel "Preview" --version $NetCoreApp10Version --shared-runtime $DotNetInstallVerbosity
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to install 1.0 shared framework"
      return $LASTEXITCODE
    fi
  fi

  # Put the stage 0 on the path
  export PATH="$DotNetRoot:$PATH"

  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  export DOTNET_MULTILEVEL_LOOKUP=0
}

function MakeGlobalSdkAvailableLocal {
  RepoToolsetSource="$DefaultNuGetPackageRoot/roslyntools.repotoolset/$ToolsetVersion/"
  RepoToolsetDestination="$NuGetPackageRoot/roslyntools.repotoolset/$ToolsetVersion/"
  if [ ! -d "$RepoToolsetDestination" ]
  then
    cp -r $RepoToolsetSource $RepoToolsetDestination
  fi
}

function InstallToolset {
  if $ci || $log
  then
    CreateDirectory $LogDir
    logCmd="/bl:$LogDir/Build.binlog"
  else
    logCmd=""
  fi

  if [ ! -d "$ToolsetBuildProj" ]
  then
    ToolsetProj="$TempDir/_restore.csproj"
    CreateDirectory $TempDir
    echo '<Project Sdk="RoslynTools.RepoToolset"><Target Name="NoOp"/></Project>' > $ToolsetProj 
    dotnet msbuild $ToolsetProj /t:NoOp /m /nologo /clp:Summary /warnaserror /p:NuGetPackageRoot=$NuGetPackageRoot /v:$verbosity $logCmd
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to build $ToolsetProj"
      return $LASTEXITCODE
    fi
  fi
}

function Build {
  InstallDotNetCli

  if [ $? != 0 ]
  then
    return $?
  fi

  InstallToolset

  if [ $? != 0 ]
  then
    return $?
  fi

  MakeGlobalSdkAvailableLocal  

  if [ $? != 0 ]
  then
    return $?
  fi

  if $prepareMachine
  then
    CreateDirectory "$NuGetPackageRoot"
    dotnet nuget locals all --clear
    LASTEXITCODE=$?

    if [ $LASTEXITCODE != 0 ]
    then
      echo "Failed to clear NuGet cache"
      return $LASTEXITCODE
    fi
  fi

  if $ci || $log
  then
    CreateDirectory $LogDir
    logCmd="/bl:$LogDir/Build.binlog"
  else
    logCmd=""
  fi

  if [ -z $solution ]
  then
    solution="$RepoRoot/Arcade.sln"
  fi

echo "dotnet msbuild $ToolsetBuildProj /m /nologo /clp:Summary /warnaserror /v:$verbosity $logCmd /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci /p:RestorePackagesPath=$NuGetPackageRoot /p:NuGetPackageRoot=$NuGetPackageRoot $properties"
  
  dotnet msbuild $ToolsetBuildProj /m /nologo /flp:v=diag /clp:Summary /warnaserror /v:$verbosity $logCmd /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci /p:RestorePackagesPath=$NuGetPackageRoot /p:NuGetPackageRoot=$NuGetPackageRoot $properties
  LASTEXITCODE=$?

  if [ $LASTEXITCODE != 0 ]
  then
    echo "Failed to build $ToolsetBuildProj"
    return $LASTEXITCODE
  fi
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 "msbuild"
  pkill -9 "vbcscompiler"
}

SOURCE="${BASH_SOURCE[0]}"
while [ -h "$SOURCE" ]; do # resolve $SOURCE until the file is no longer a symlink
  ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"
  SOURCE="$(readlink "$SOURCE")"
  [[ $SOURCE != /* ]] && SOURCE="$ScriptRoot/$SOURCE" # if $SOURCE was a relative symlink, we need to resolve it relative to the path where the symlink file was located
done
ScriptRoot="$( cd -P "$( dirname "$SOURCE" )" && pwd )"

RepoRoot="$ScriptRoot/../"
ArtifactsDir="$RepoRoot/artifacts"
ArtifactsConfigurationDir="$ArtifactsDir/$configuration"
LogDir="$ArtifactsConfigurationDir/log"
GlobalJsonFile="$RepoRoot/global.json"

readJson $GlobalJsonFile "RoslynTools.RepoToolset"
ToolsetVersion=$JsonValue
TempDir="$ArtifactsConfigurationDir/tmp"

# HOME may not be defined in some scenarios, but it is required by NuGet
if [ -z $HOME ]
then
  export HOME="$RepoRoot/artifacts/.home/"
  CreateDirectory "$HOME"
fi

if $ci
then
  CreateDirectory "$TempDir"

  export TEMP="$TempDir"
  export TMP="$TempDir"
fi

if [ -z $NUGET_PACKAGES ]
then
  export NUGET_PACKAGES="$RepoRoot/packages"
fi

NuGetPackageRoot=$NUGET_PACKAGES
DefaultNuGetPackageRoot="$HOME/.nuget/packages"

ToolsetBuildProj="$NuGetPackageRoot/roslyntools.repotoolset/$ToolsetVersion/tools/Build.proj"

Build
LASTEXITCODE=$?

if $ci && $prepareMachine
then
  StopProcesses
fi

exit $LASTEXITCODE