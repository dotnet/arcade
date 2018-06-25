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
test=false
publish=false
verbosity='minimal'
properties=()
reporoot="$scriptroot/../.."
artifactsdir="$reporoot/artifacts"
logdir="$artifactsdir/log"
globaljsonfile="$reporoot/global.json"
tempdir="$artifactsdir/tmp"
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
      echo "  --publish        Publish built packages"
      echo ""
      echo "Advanced settings:"
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
    --publish)
      publish=true
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
    --test)
      test=true
      shift 1
      ;;
    --verbosity)
      verbosity=$2
      shift 2
      ;;
    *)
      properties+=($1)
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
  local dotnetinstallverbosity=''

  ReadJson "$globaljsonfile" "version"
  local dotnetcliversion="$readjsonvalue"

  if [[ -z "$DOTNET_INSTALL_DIR" ]]; then
    export DOTNET_INSTALL_DIR="$reporoot/.dotnet/$dotnetcliversion"
  fi

  local dotnetroot="$DOTNET_INSTALL_DIR"
  local dotnetinstallscript="$dotnetroot/dotnet-install.sh"

  if [[ ! -a "$dotnetinstallscript" ]]; then
    mkdir -p "$dotnetroot"

    # Use curl if available, otherwise use wget
    if command -v curl > /dev/null; then
      curl "https://dot.net/v1/dotnet-install.sh" -sSL --retry 10 --create-dirs -o "$dotnetinstallscript"
    else
      wget -q -O "$dotnetinstallscript" "https://dot.net/v1/dotnet-install.sh"
    fi
  fi

  if [[ "$(echo $verbosity | awk '{print tolower($0)}')" == 'diagnostic' ]]; then
    dotnetinstallverbosity="--verbose"
  fi

  # Install a stage 0
  local sdkinstalldir="$dotnetroot/sdk/$dotnetcliversion"

  if [[ ! -d "$sdkinstalldir" ]]; then
    bash "$dotnetinstallscript" --version $dotnetcliversion $dotnetinstallverbosity
    local lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install stage0"
      ExitWithExitCode $lastexitcode
    fi
  fi

  # Install 1.0 shared framework
  local netcoreappversion='1.0.5'
  local netcoreapp10dir="$dotnetroot/shared/Microsoft.NETCore.App/$netcoreappversion"

  if [[ ! -d "$netcoreapp10dir" ]]; then
    bash "$dotnetinstallscript" --channel "Preview" --version $netcoreappversion --shared-runtime $dotnetinstallverbosity
    lastexitcode=$?

    if [[ $lastexitcode != 0 ]]; then
      echo "Failed to install 1.0 shared framework"
      ExitWithExitCode $lastexitcode
    fi
  fi

  # Put the stage 0 on the path
  export PATH="$dotnetroot:"$PATH""

  # Disable first run since we want to control all package sources
  export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

  # Don't resolve runtime, shared framework, or SDK from other locations
  export DOTNET_MULTILEVEL_LOOKUP=0
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

  local msbuildArgs=()
  msbuildArgs+=("$reporoot/build.proj")
  msbuildArgs+=("/m")
  msbuildArgs+=("/nologo")
  msbuildArgs+=("/clp:Summary")
  msbuildArgs+=("/warnaserror")
  msbuildArgs+=("/v:$verbosity")
  msbuildArgs+=("/p:Configuration=$configuration")
  msbuildArgs+=("/p:CIBuild=$ci")

  if [[ "$ci" == true || "$log" == true ]] ; then
    mkdir -p $logdir
    msbuildArgs+=("/bl:$logdir/Build.binlog")
  fi

  local targets=()

  if [[ "$rebuild" == true ]] ; then
    targets+=("Rebuild")
  elif [[ "$build" == true ]] ; then
    targets+=("Build")
  fi

  if [[ "$test" == true ]] ; then
    targets+=("Test")
  fi

  if [[ "$pack" == true ]] ; then
    targets+=("Pack")
  fi

  if [[ "$sign" == true ]] ; then
    targets+=("Sign")
  fi

  if [[ "$publish" == true ]] ; then
    targets+=("Publish")
  fi

  targets=$(IFS=\; ; echo "${targets[*]}")

  if [[ "$restore" == true ]] ; then
    if [ -z "$targets" ] ; then
      targets="Restore"
    else
      msbuildArgs+=("/restore")
    fi
  fi

  dotnet msbuild "/t:$targets" "${msbuildArgs[@]}" "${properties[@]}"
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

  Build
  ExitWithExitCode $?
}

Main
