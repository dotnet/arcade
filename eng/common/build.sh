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
log=false
pack=false
preparemachine=false
rebuild=false
restore=false
sign=false
projects=''
test=false
verbosity='minimal'
properties=''
reporoot="$scriptroot/../.."
artifactsdir="$reporoot/artifacts"
artifactsconfigurationdir="$artifactsdir/$configuration"
logdir="$artifactsconfigurationdir/log"
globaljsonfile="$reporoot/global.json"
tempdir="$artifactsconfigurationdir/tmp"
officialbuild=false

if [[ ! -z $OfficialBuildId ]]; then
  officialbuild=true
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
      echo "  --projects <value>     Semi-colon delimited list of sln/proj's to build. Globbing is supported (*.sln)"
      echo "  --ci           Set when running on CI server"
      echo "  --log          Enable logging (by default on CI)"
      echo "  --preparemachine     Prepare machine for CI run"
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
    --preparemachine)
      preparemachine=true
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

function InstallDotNetCli {
  bash "$scriptroot/get-dotnet-cli.sh"
}

# This is a temporary workaround for https://github.com/Microsoft/msbuild/issues/2095 and
# https://github.com/dotnet/cli/issues/6589
# Currently, SDK's always get resolved to the global location, but we want our packages
# to all be installed into a local folder (prevent machine contamination from global state).
#
# We are restoring all of our packages locally and setting nugetpackageroot to reference the
# local location, but this breaks Custom SDK's which are expecting the SDK to be available
# from the global user folder.
function MakeGlobalSdkAvailableLocal {
  local repotoolsetsource="$defaultnugetpackageroot/roslyntools.repotoolset/$toolsetversion/"
  local repotoolsetdestination="$nugetpackageroot/roslyntools.repotoolset/$toolsetversion/"
  if [[ ! -d "$repotoolsetdestination" ]]; then
    cp -r $repotoolsetsource $repotoolsetdestination
  fi
}

function InstallToolset {
  if [[ ! -d "$toolsetbuildproj" ]]; then
    local toolsetproj="$tempdir/_restore.csproj"
    mkdir -p "$tempdir"
    echo '<Project Sdk="RoslynTools.RepoToolset"><Target Name="NoOp"/></Project>' > $toolsetproj

    dotnet msbuild $toolsetproj /t:NoOp /m /nologo /clp:Summary /warnaserror "/p:NuGetPackageRoot=$nugetpackageroot/" /v:$verbosity
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to build $toolsetproj"
      ExitWithExitCode $lastexitcode
    fi
  fi
}

function Build {
  InstallDotNetCli

  if [[ "$preparemachine" == true ]]; then
    mkdir -p "$nugetpackageroot"
    dotnet nuget locals all --clear
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo 'Failed to clear NuGet cache'
      ExitWithExitCode $lastexitcode
    fi
  fi

  InstallToolset

  if [[ "$officialbuild" == true ]]; then
    MakeGlobalSdkAvailableLocal
  fi

  local logcmd=''
  if [[ "$ci" == true || "$log" == true ]] ; then
    mkdir -p $logdir
    logcmd="/bl:$logdir/Build.binlog"
  fi

  if [[ -z $projects ]]; then
    projects="$reporoot/*.sln"
  fi

  dotnet msbuild $toolsetbuildproj /m /nologo /clp:Summary /warnaserror \
    /v:$verbosity $logcmd /p:Configuration=$configuration /p:RepoRoot=$reporoot /p:Projects=$projects \
    /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Deploy=$deploy /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci \
    "/p:RestorePackagesPath=$nugetpackageroot/" "/p:NuGetPackageRoot=$nugetpackageroot/" \
    $properties
  local lastexitcode=$?

  if [[ $lastexitcode != 0 ]]; then
    echo "Failed to build $toolsetbuildproj"
    ExitWithExitCode $lastexitcode
  fi
}

function ExitWithExitCode {
  if [[ "$ci" == true && "$preparemachine" == true ]]; then
    StopProcesses
  fi
  exit $1
}

function StopProcesses {
  echo "Killing running build processes..."
  pkill -9 "msbuild"
  pkill -9 "vbcscompiler"
}

function Main {
  # HOME may not be defined in some scenarios, but it is required by NuGet
  if [[ -z $HOME ]]; then
    export HOME="$reporoot/artifacts/.home/"
    mkdir -p "$HOME"
  fi

  if [[ $ci ]]; then
    mkdir -p "$tempdir"
    export TEMP="$tempdir"
    export TMP="$tempdir"
  fi
  
  if [[ -z $NUGET_PACKAGES ]]; then
    if [[ "$officialbuild" == true ]]; then
      export NUGET_PACKAGES="$reporoot/packages"
    else
      export NUGET_PACKAGES="$HOME/.nuget/packages"
    fi
  fi
  nugetpackageroot=$NUGET_PACKAGES
  defaultnugetpackageroot="$HOME/.nuget/packages"

  ReadJson $globaljsonfile "RoslynTools.RepoToolset"
  toolsetversion=$readjsonvalue

  toolsetbuildproj="$nugetpackageroot/roslyntools.repotoolset/$toolsetversion/tools/Build.proj"

  Build
  ExitWithExitCode $?
}

Main
