#!/usr/bin/env bash

source="${BASH_SOURCE[0]}"

# resolve $source until the file is no longer a symlink
while [[ -h "$source" ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"
  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

build=false
ci=false
configuration='Debug'
help=false
pack=false
prepare_machine=false
rebuild=false
restore=false
sign=false
projects=''
test=false
verbosity='minimal'
properties=''
repo_root="$scriptroot/../.."
artifacts_dir="$repo_root/artifacts"
artifacts_configuration_dir="$artifacts_dir/$configuration"
log_dir="$artifacts_configuration_dir/log"
log="$log_dir/Build.binlog"
global_json_file="$repo_root/global.json"
temp_dir="$artifacts_configuration_dir/tmp"
official_build=false
build_driver=""

if [[ ! -z $OfficialBuildId ]]; then
  official_build=true
fi

while (($# > 0)); do
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
      echo "  --verbosity <value>      Msbuild verbosity (q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic])"
      echo "  --help                   Print help and exit"
      echo ""
      echo "Actions:"
      echo "  --restore                Restore dependencies"
      echo "  --build                  Build solution"
      echo "  --rebuild                Rebuild solution"
      echo "  --test                   Run all unit tests in the solution"
      echo "  --sign                   Sign build outputs"
      echo "  --pack                   Package build outputs into NuGet packages and Willow components"
      echo ""
      echo "Advanced settings:"
      echo "  --projects <value>     Semi-colon delimited list of sln/proj's to build. Globbing is supported (*.sln)"
      echo "  --ci                     Set when running on CI server"
      echo "  --prepareMachine         Prepare machine for CI run"
      echo ""
      echo "Command line arguments not listed above are passed through to MSBuild."
      exit 0
      ;;
    --pack)
      pack=true
      shift 1
      ;;
    --preparemachine)
      prepare_machine=true
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
    --projects)
      projects=$2
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

# ReadJson [filename] [json key]
# Result: Sets 'readjsonvalue' to the value of the provided json key
# Note: this method may return unexpected results if there are duplicate
# keys in the json
function ReadJson {
  local unamestr="$(uname)"
  local sedextended='-r'
  if [[ "$unamestr" == 'Darwin' ]]; then
    sedextended='-E'
  fi;

  readjsonvalue="$(grep -m 1 "\"${2}\"" ${1} | sed ${sedextended} 's/^ *//;s/.*: *"//;s/",?//')"
  if [[ ! "$readjsonvalue" ]]; then
    echo "Error: Cannot find \"${2}\" in ${1}" >&2;
    ExitWithExitCode 1
  fi;
}

function InitializeDotNetCli {
  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations to ensure build determinism
  export DOTNET_MULTILEVEL_LOOKUP=0

  # Source Build uses DotNetCoreSdkDir variable
  if [[ -n "$DotNetCoreSdkDir" ]]; then
    export DOTNET_INSTALL_DIR="$DotNetCoreSdkDir"
  fi

  local dotnet_root=""

  # Use dotnet installation specified in DOTNET_INSTALL_DIR if it contains the required SDK version, 
  # otherwise install the dotnet CLI and SDK to repo local .dotnet directory to avoid potential permission issues.
  if [[ -d "$DOTNET_INSTALL_DIR/sdk/$dotnet_sdk_version" ]]; then
    dotnet_root="$DOTNET_INSTALL_DIR"
  else
    dotnet_root="$repo_root/.dotnet"
    export DOTNET_INSTALL_DIR="$dotnet_root"

    if [[ "$restore" == true ]]; then
      InstallDotNetCli $dotnet_root
    fi
  fi

  build_driver="$dotnet_root/dotnet"
}

function InstallDotNetCli {
  local dotnet_root=$1
  local dotnet_install_script="$dotnet_root/dotnet-install.sh"

  if [[ ! -a "$dotnet_install_script" ]]; then
    mkdir -p "$dotnet_root"

    # Use curl if available, otherwise use wget
    if command -v curl > /dev/null; then
      curl "https://dot.net/v1/dotnet-install.sh" -sSL --retry 10 --create-dirs -o "$dotnet_install_script"
    else
      wget -q -O "$dotnet_install_script" "https://dot.net/v1/dotnet-install.sh"
    fi
  fi

  bash "$dotnet_install_script" --version $dotnet_sdk_version
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to install stage0"
    ExitWithExitCode $lastexitcode
  fi
}

# This is a temporary workaround for https://github.com/Microsoft/msbuild/issues/2095 and
# https://github.com/dotnet/cli/issues/6589
# Currently, SDK's always get resolved to the global location, but we want our packages
# to all be installed into a local folder (prevent machine contamination from global state).
#
# We are restoring all of our packages locally and setting nuget_package_root to reference the
# local location, but this breaks Custom SDK's which are expecting the SDK to be available
# from the global user folder.
function MakeGlobalSdkAvailableLocal {
  local repotoolsetsource="$default_nuget_package_root/roslyntools.repotoolset/$toolset_version/"
  local repotoolsetdestination="$nuget_package_root/roslyntools.repotoolset/$toolset_version/"
  if [[ ! -d "$repotoolsetdestination" ]]; then
    cp -r $repotoolsetsource $repotoolsetdestination
  fi
}

function InitializeToolset {
  if [[ ! -d "$toolset_build_proj" ]]; then
    local toolset_proj="$temp_dir/_restore.csproj"
    echo '<Project Sdk="RoslynTools.RepoToolset"><Target Name="NoOp"/></Project>' > $toolset_proj

    "$build_driver" msbuild $toolset_proj /t:NoOp /m /nologo /clp:Summary /warnaserror /p:NuGetPackageRoot="$nuget_package_root/" /v:$verbosity
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to build $toolset_proj"
      ExitWithExitCode $lastexitcode
    fi
  fi
}

function PrepareMachine {
  if [[ "$prepare_machine" == true ]]; then
    mkdir -p "$nuget_package_root"
    "$build_driver" nuget locals all --clear
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo 'Failed to clear NuGet cache'
      ExitWithExitCode $lastexitcode
    fi
  fi
}

function Build {
  if [[ "$official_build" == true ]]; then
    MakeGlobalSdkAvailableLocal
  fi

  "$build_driver" msbuild $toolset_build_proj /m /nologo /clp:Summary /warnaserror \
    /v:$verbosity /bl:$log /p:Configuration=$configuration /p:Projects=$projects /p:RepoRoot="$repo_root" \
    /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci \
    /p:RestorePackagesPath="$nuget_package_root/" /p:NuGetPackageRoot="$nuget_package_root/" \
    $properties
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to build $toolset_build_proj"
    ExitWithExitCode $lastexitcode
  fi
}

function ExitWithExitCode {
  if [[ "$ci" == true && "$prepare_machine" == true ]]; then
    StopProcesses
  fi
  exit $1
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 "dotnet"
  pkill -9 "vbcscompiler"
}

function Main {
  # HOME may not be defined in some scenarios, but it is required by NuGet
  if [[ -z $HOME ]]; then
    export HOME="$repo_root/artifacts/.home/"
    mkdir -p "$HOME"
  fi

  if [[ -z $projects ]]; then
    projects="$repo_root/*.sln"
  fi

  if [[ -z $NUGET_PACKAGES ]]; then
    if [[ "$official_build" == true ]]; then
      export NUGET_PACKAGES="$repo_root/packages"
    else
      export NUGET_PACKAGES="$HOME/.nuget/packages"
    fi
  fi
  nuget_package_root=$NUGET_PACKAGES
  default_nuget_package_root="$HOME/.nuget/packages"

  mkdir -p "$temp_dir"
  mkdir -p "$log_dir"
  
  if [[ $ci ]]; then
    mkdir -p "$temp_dir"
    export TEMP="$temp_dir"
    export TMP="$temp_dir"
  fi

  ReadJson $global_json_file "RoslynTools.RepoToolset"
  toolset_version=$readjsonvalue

  ReadJson "$global_json_file" "version"
  dotnet_sdk_version="$readjsonvalue"

  toolset_build_proj="$nuget_package_root/roslyntools.repotoolset/$toolset_version/tools/Build.proj"

  InitializeDotNetCli
  PrepareMachine
  InitializeToolset

  Build
  ExitWithExitCode $?
}

Main