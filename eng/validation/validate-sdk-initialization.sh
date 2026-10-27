#!/usr/bin/env bash
set -euo pipefail

if [[ $# != 3 ]]; then
  echo "Usage: $0 <dotnet-root> <repository-sdk-version> <tools-sdk-version>" >&2
  exit 1
fi

dotnet_source=$(cd "$1" && pwd)
repository_sdk=$2
tools_sdk=$3
for version in "$repository_sdk" "$tools_sdk"; do
  [[ -d "$dotnet_source/sdk/$version" ]] || { echo "SDK '$version' must already be installed." >&2; exit 1; }
done

disable_configure_toolset_import=1
source "$(dirname "${BASH_SOURCE[0]}")/../common/tools.sh"
root=$(mktemp -d)
trap 'rm -rf -- "$root"' EXIT

add_sdk() {
  local destination=$1 version=$2
  mkdir -p "$destination/sdk"
  if [[ ! -e "$destination/dotnet" ]]; then
    cp "$dotnet_source/dotnet" "$destination/dotnet"
    ln -s "$dotnet_source/host" "$destination/host"
    ln -s "$dotnet_source/shared" "$destination/shared"
  fi
  ln -s "$dotnet_source/sdk/$version" "$destination/sdk/$version"
}

# Stage existing SDK files rather than downloading them; SDK resolution uses the real host.
function InstallDotNetSdk {
  installs+=("$2")
  add_sdk "$1" "$2"
}

for scenario in matching no-sdk missing both major latestMajor no-install path global-fallback runtimes cached local-major both-latestMajor; do
  (
    repo_root="$root/$scenario/"
    external="${repo_root}external"
    fallback="${repo_root}.dotnet"
    add_sdk "$external" "$tools_sdk"
    repo_version=$repository_sdk
    [[ "$scenario" != matching ]] || repo_version=$tools_sdk
    roll_forward=''
    [[ "$scenario" != major && "$scenario" != latestMajor ]] || roll_forward=",\"rollForward\":\"$scenario\""
    [[ "$scenario" != local-major ]] || roll_forward=',"rollForward":"major"'
    [[ "$scenario" != both-latestMajor ]] || roll_forward=',"rollForward":"latestMajor"'
    global_json_file="${repo_root}global.json"
    if [[ "$scenario" == no-sdk ]]; then
      printf '{"tools":{"dotnet":"%s"}}' "$tools_sdk" > "$global_json_file"
    else
      printf '{"sdk":{"version":"%s","allowPrerelease":true%s},"tools":{"dotnet":"%s"}}' \
        "$repo_version" "$roll_forward" "$tools_sdk" > "$global_json_file"
    fi
    [[ "$scenario" != both && "$scenario" != both-latestMajor ]] || add_sdk "$external" "$repository_sdk"
    [[ "$scenario" != local-major ]] || add_sdk "$fallback" "$tools_sdk"
    global_json_has_runtimes=false
    [[ "$scenario" != runtimes ]] || global_json_has_runtimes=true
    use_installed_dotnet_cli=true
    [[ "$scenario" != local-major ]] || use_installed_dotnet_cli=false
    export PATH="$external:$PATH"
    unset _InitializeDotNetCli DOTNET_INSTALL_DIR DOTNET_GLOBAL_INSTALL_DIR
    [[ "$scenario" == path || "$scenario" == local-major ]] || export DOTNET_INSTALL_DIR="$external"
    [[ "$scenario" != global-fallback ]] || export DOTNET_GLOBAL_INSTALL_DIR="$fallback"
    installs=()
    install=true
    [[ "$scenario" != no-install ]] || install=false
    reuse=false
    case "$scenario" in matching|no-sdk|both|major|latestMajor|both-latestMajor|no-install) reuse=true ;; esac
    no_installs=$reuse
    [[ "$scenario" != local-major ]] || no_installs=true
    expected_root=$fallback
    [[ "$reuse" != true ]] || expected_root=$external

    InitializeDotNetCli "$install"
    [[ "$_InitializeDotNetCli" == "$expected_root" ]]
    if [[ "$no_installs" == true ]]; then
      [[ ${#installs[@]} == 0 ]]
    else
      [[ "${installs[*]}" == "$tools_sdk $repository_sdk" ]]
    fi
    if [[ "$scenario" == cached ]]; then
      installs=()
      InitializeDotNetCli true
      [[ "$_InitializeDotNetCli" == "$expected_root" && ${#installs[@]} == 0 ]]
    fi
    if [[ "$install" == true ]]; then
      expected_version=$repository_sdk
      [[ "$no_installs" != true || "$scenario" == both ]] || expected_version=$tools_sdk
      cd "$repo_root"
      [[ "$("$_InitializeDotNetCli/dotnet" --version)" == "$expected_version" ]]
    fi
    echo "PASS: $scenario"
  )
done
