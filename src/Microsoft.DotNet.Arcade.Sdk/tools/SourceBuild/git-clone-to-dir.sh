#!/usr/bin/env bash

usage() {
  echo "$0 [options]"
  echo "Creates a git clone of <source> into <dest> directory, optionally saving WIP changes."
  echo ""
  echo "Required:"
  echo "  --source <dir>  (Required) The source Git directory."
  echo "  --dest <dir>    (Required) The destination Git directory. Created if doesn't exist."
  echo ""
  echo "Optional:"
  echo "  --clean     If dest directory already exists, delete it."
  echo "  --copy-wip  Transfer most types of uncommitted change into the"
  echo "              destination directory. Useful for dev workflows."
  echo "  -h --help   Print help and exit."
}

# An alternative to "git clone" would be to use a "git worktree". Potential benefits:
# * This has more integration with the user's normal repo. If they make ad-hoc changes in the
#   worktree, it is easy to cherry-pick onto the developer's "real" branch.
# * A worktree uses a '.git' file rather than a full '.git' directory, which might have storage
#   implications. (However, a local clone's '.git' directory uses hard links to save space/time,
#   so it's not certain that this affects performance at all.)
# 
# Downside of worktrees:
# * Some configuration is set up in '.git/worktrees' which may be difficult to coordinate
#   properly. In particular, if the user is already using worktrees for their own purposes, we
#   would have to be careful that running source-build in one worktree doesn't interfere with
#   source-build in the other worktree.

set -euo pipefail

while [[ $# > 0 ]]; do
  opt="$(echo "$1" | awk '{print tolower($0)}')"
  case "$opt" in
    -h|--help)
      usage
      exit 0
      ;;
    --source)
      sourceDir="$2"
      shift
      ;;
    --dest)
      destDir="$2"
      shift
      ;;
    --clean)
      clean=1
      ;;
    --copy-wip)
      copyWip=1
      ;;
    *)
      echo "Unrecognized option: $1"
      echo ""
      usage
      exit 1
      ;;
  esac
  shift
done

[ ! -d "${sourceDir:-}" ] && echo "--source not a directory: $sourceDir" && exit 1

[ ! "${destDir:-}" ] && echo "--dest not specified" && exit 1

if [ -e "$destDir" ]; then
  echo "Destination already exists!"
  if [ -f "$destDir" ]; then
    echo "Existing destination is a regular file, not a directory. This is unusual: aborting."
    exit 1
  elif [ "${clean:-}" ]; then
    echo "Clean is enabled: removing $destDir and continuing..."
    rm -rf "$destDir"
  else
    echo "Clean is not enabled: aborting."
    exit 1
  fi
fi

if [ ! -e "$destDir" ]; then
  mkdir -p "$destDir"

  if [ "${copyWip:-}" ]; then
    echo "WIP copying is enabled"
    # Copy over changes that haven't been committed, for dev inner loop.
    # This gets changes (whether staged or not) but misses untracked files.
    stashCommit=$(cd "$sourceDir"; git stash create)

    if [ "$stashCommit" ]; then
      echo "Created temporary stash $stashCommit to transfer WIP changes..."
    fi
  fi

  echo "Creating empty clone at: $destDir"
  git clone --no-checkout "$sourceDir" "$destDir"

  (
    cd "$destDir"
    echo "Checking out sources..."
    # If no changes were stashed, stashCommit is empty string, and this is a simple checkout.
    git checkout ${stashCommit:-}
  )

  echo "Clone complete: $sourceDir -> $destDir"
fi
