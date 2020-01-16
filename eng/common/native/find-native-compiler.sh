#!/usr/bin/env bash
#
# This file locates the native compiler with the given name and version and sets the environment variables to locate it.
#

source="${BASH_SOURCE[0]}"

# resolve $SOURCE until the file is no longer a symlink
while [[ -h $source ]]; do
  scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"
  source="$(readlink "$source")"

  # if $source was a relative symlink, we need to resolve it relative to the path where the
  # symlink file was located
  [[ $source != /* ]] && source="$scriptroot/$source"
done
scriptroot="$( cd -P "$( dirname "$source" )" && pwd )"

if [ $# -lt 4 ]
then
  echo "Usage..."
  echo "gen-buildsys.sh <compiler> <compiler major version> <compiler minor version>"
  echo "Specify the name of compiler (clang or gcc)."
  echo "Specify the major version of compiler."
  echo "Specify the minor version of compiler."
  exit 1
fi

compiler="$1"
cxxCompiler="$compiler++"
majorVersion="$2"
minorVersion="$3"

if [ "$compiler" = "gcc" ]; then cxxCompiler="g++"; fi

check_version_exists() {
    desired_version=-1

    # Set up the environment to be used for building with the desired compiler.
    if command -v "$compiler-$1.$2" > /dev/null; then
        desired_version="-$1.$2"
    elif command -v "$compier$1$2" > /dev/null; then
        desired_version="$1$2"
    elif command -v "$compiler-$1$2" > /dev/null; then
        desired_version="-$1$2"
    fi

    echo "$desired_version"
}

if [ -z "$CLR_CC" ]; then

    # Set default versions
    if [ -z "$majorVersion" ]; then
        # note: gcc (all versions) and clang versions higher than 6 do not have minor version in file name, if it is zero.
        if [ "$compiler" = "clang" ]; then versions=( 9 8 7 6.0 5.0 4.0 3.9 3.8 3.7 3.6 3.5 )
        elif [ "$compiler" = "gcc" ]; then versions=( 9 8 7 6 5 4.9 ); fi

        for version in "${versions[@]}"; do
            parts=(${version//./ })
            desired_version="$(check_version_exists "${parts[0]}" "${parts[1]}")"
            if [ "$desired_version" != "-1" ]; then majorVersion="${parts[0]}"; break; fi
        done

        if [ -z "$majorVersion" ]; then
            if command -v "$compiler" > /dev/null; then
                if [ "$(uname)" != "Darwin" ]; then
                    echo "WARN: Specific version of $compiler not found, falling back to use the one in PATH."
                fi
                export CC="$(command -v "$compiler")"
                export CXX="$(command -v "$cxxCompiler")"
            else
                echo "ERROR: No usable version of $compiler found."
                exit 1
            fi
        else
            if [ "$compiler" = "clang" ] && [ "$majorVersion" -lt 5 ]; then
                if [ "$build_arch" = "arm" ] || [ "$build_arch" = "armel" ]; then
                    if command -v "$compiler" > /dev/null; then
                        echo "WARN: Found clang version $majorVersion which is not supported on arm/armel architectures, falling back to use clang from PATH."
                        export CC="$(command -v "$compiler")"
                        export CXX="$(command -v "$cxxCompiler")"
                    else
                        echo "ERROR: Found clang version $majorVersion which is not supported on arm/armel architectures, and there is no clang in PATH."
                        exit 1
                    fi
                fi
            fi
        fi
    else
        desired_version="$(check_version_exists "$majorVersion" "$minorVersion")"
        if [ "$desired_version" = "-1" ]; then
            echo "ERROR: Could not find specific version of $compiler: $majorVersion $minorVersion."
            exit 1
        fi
    fi

    if [ -z "$CC" ]; then
        export CC="$(command -v "$compiler$desired_version")"
        export CXX="$(command -v "$cxxCompiler$desired_version")"
        if [ -z "$CXX" ]; then export CXX="$(command -v "$cxxCompiler")"; fi
    fi
else
    if [ ! -f "$CLR_CC" ]; then
        echo "ERROR: CLR_CC is set but path '$CLR_CC' does not exist"
        exit 1
    fi
    export CC="$CLR_CC"
    export CXX="$CLR_CXX"
fi

if [ -z "$CC" ]; then
    echo "ERROR: Unable to find $compiler."
    exit 1
fi

export CCC_CC="$CC"
export CCC_CXX="$CXX"
export SCAN_BUILD_COMMAND="$(command -v "scan-build$desired_version")"
