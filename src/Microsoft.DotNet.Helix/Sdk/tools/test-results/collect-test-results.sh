#!/bin/sh

# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.

set -eu

source_root=$(pwd -P)
mkdir -p "${HELIX_WORKITEM_UPLOAD_ROOT:?HELIX_WORKITEM_UPLOAD_ROOT is required}"
upload_root=$(cd "$HELIX_WORKITEM_UPLOAD_ROOT" && pwd -P)

find "$source_root" -type d -path "$upload_root" -prune -o -type f \
    \( -iname '*.trx' -o -iname '*testResults.xml' -o -iname '*test-results.xml' \
       -o -iname '*test_results.xml' -o -iname '*junit-results.xml' -o -iname '*junitresults.xml' \) \
    -exec sh -c '
        upload_root=$1
        source_root=$2
        shift 2
        for file do
            relative_path=${file#"$source_root"/}
            destination="$upload_root/$relative_path"
            mkdir -p "$(dirname "$destination")" && cp -f "$file" "$destination" || exit 1
        done
    ' sh "$upload_root" "$source_root" {} +
