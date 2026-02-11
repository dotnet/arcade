#!/bin/bash
set -euo pipefail

# This script adds a symbolic link (NativeLibrary-link.dll -> NativeLibrary.dll)
# to the Payload of NestedPkg.pkg and test.pkg.
# Must be run on macOS from the Resources directory.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

if [[ "$(uname)" != "Darwin" ]]; then
    echo "Error: This script must be run on macOS."
    exit 1
fi

SYMLINK_NAME="NativeLibrary-link.dll"
SYMLINK_TARGET="NativeLibrary.dll"
WORK_DIR="$(mktemp -d)"

cleanup() {
    rm -rf "$WORK_DIR"
}
trap cleanup EXIT

echo "Working directory: $WORK_DIR"

# Helper: read an attribute from a PackageInfo XML file
read_pkg_attr() {
    local file="$1" attr="$2"
    # Extract attribute value from the <pkg-info> element
    python3 -c "
import xml.etree.ElementTree as ET
tree = ET.parse('$file')
print(tree.getroot().get('$attr'))
"
}

# Helper: rebuild a component package with a symlink added to its payload
rebuild_component_with_symlink() {
    local pkg_path="$1"
    local expand_dir="$WORK_DIR/expand-$$-$RANDOM"
    local payload_dir="$WORK_DIR/payload-$$-$RANDOM"

    # Expand the component package
    pkgutil --expand "$pkg_path" "$expand_dir"

    # Extract payload contents
    mkdir "$payload_dir"
    (cd "$payload_dir" && cat "$expand_dir/Payload" | gunzip | cpio -id 2>/dev/null)

    # Add symlink
    ln -s "$SYMLINK_TARGET" "$payload_dir/$SYMLINK_NAME"
    echo "  Added symlink: $SYMLINK_NAME -> $SYMLINK_TARGET"

    # Read PackageInfo attributes
    local pkg_info="$expand_dir/PackageInfo"
    local identifier version install_location
    identifier="$(read_pkg_attr "$pkg_info" "identifier")"
    version="$(read_pkg_attr "$pkg_info" "version")"
    install_location="$(read_pkg_attr "$pkg_info" "install-location")"
    echo "  Package: identifier=$identifier version=$version install-location=$install_location"

    # Build pkgbuild args
    local pkgbuild_args=(--root "$payload_dir" --identifier "$identifier" --version "$version" --install-location "$install_location")

    # Include Scripts directory if present
    if [[ -d "$expand_dir/Scripts" ]]; then
        pkgbuild_args+=(--scripts "$expand_dir/Scripts")
    fi

    # Rebuild
    local rebuilt="$WORK_DIR/rebuilt-$$-$RANDOM.pkg"
    pkgbuild "${pkgbuild_args[@]}" "$rebuilt" >/dev/null 2>&1
    echo "  Rebuilt package successfully"

    # Replace original
    mv "$rebuilt" "$pkg_path"

    # Cleanup intermediate dirs
    rm -rf "$expand_dir" "$payload_dir"
}

# --- Step 1: Update NestedPkg.pkg ---
echo ""
echo "=== Updating NestedPkg.pkg ==="
rebuild_component_with_symlink "NestedPkg.pkg"

# --- Step 2: Update test.pkg (installer package containing NestedPkg.pkg) ---
echo ""
echo "=== Updating test.pkg ==="
INSTALLER_EXPAND="$WORK_DIR/test-expand"
pkgutil --expand test.pkg "$INSTALLER_EXPAND"

# The nested NestedPkg.pkg is expanded as a directory inside test-expand.
# Extract its payload, add symlink, and rebuild it as a flat .pkg file.
NESTED_DIR="$INSTALLER_EXPAND/NestedPkg.pkg"
NESTED_PAYLOAD="$WORK_DIR/nested-payload"

mkdir "$NESTED_PAYLOAD"
(cd "$NESTED_PAYLOAD" && cat "$NESTED_DIR/Payload" | gunzip | cpio -id 2>/dev/null)

ln -s "$SYMLINK_TARGET" "$NESTED_PAYLOAD/$SYMLINK_NAME"
echo "  Added symlink: $SYMLINK_NAME -> $SYMLINK_TARGET"

# Read PackageInfo from the nested component
NESTED_PKG_INFO="$NESTED_DIR/PackageInfo"
NESTED_ID="$(read_pkg_attr "$NESTED_PKG_INFO" "identifier")"
NESTED_VER="$(read_pkg_attr "$NESTED_PKG_INFO" "version")"
NESTED_LOC="$(read_pkg_attr "$NESTED_PKG_INFO" "install-location")"
echo "  Package: identifier=$NESTED_ID version=$NESTED_VER install-location=$NESTED_LOC"

NESTED_PKGBUILD_ARGS=(--root "$NESTED_PAYLOAD" --identifier "$NESTED_ID" --version "$NESTED_VER" --install-location "$NESTED_LOC")
if [[ -d "$NESTED_DIR/Scripts" ]]; then
    NESTED_PKGBUILD_ARGS+=(--scripts "$NESTED_DIR/Scripts")
fi

NESTED_REBUILT="$WORK_DIR/nested-rebuilt.pkg"
pkgbuild "${NESTED_PKGBUILD_ARGS[@]}" "$NESTED_REBUILT" >/dev/null 2>&1

# Replace the expanded directory with the flat .pkg file
rm -rf "$NESTED_DIR"
mv "$NESTED_REBUILT" "$NESTED_DIR"

# Rebuild the installer package
PRODUCTBUILD_ARGS=(--distribution "$INSTALLER_EXPAND/Distribution" --package-path "$INSTALLER_EXPAND")
if [[ -d "$INSTALLER_EXPAND/Resources" ]]; then
    PRODUCTBUILD_ARGS+=(--resources "$INSTALLER_EXPAND/Resources")
fi

TEST_REBUILT="$WORK_DIR/test-rebuilt.pkg"
productbuild "${PRODUCTBUILD_ARGS[@]}" "$TEST_REBUILT" >/dev/null 2>&1
mv "$TEST_REBUILT" test.pkg
echo "  Rebuilt installer package successfully"

# --- Verify ---
echo ""
echo "=== Verification ==="

VERIFY_DIR="$WORK_DIR/verify"
pkgutil --expand NestedPkg.pkg "$VERIFY_DIR"
VERIFY_PAYLOAD="$WORK_DIR/verify-payload"
mkdir "$VERIFY_PAYLOAD"
(cd "$VERIFY_PAYLOAD" && cat "$VERIFY_DIR/Payload" | gunzip | cpio -id 2>/dev/null)

if [[ -L "$VERIFY_PAYLOAD/$SYMLINK_NAME" ]]; then
    ACTUAL_TARGET="$(readlink "$VERIFY_PAYLOAD/$SYMLINK_NAME")"
    echo "  NestedPkg.pkg: OK - $SYMLINK_NAME -> $ACTUAL_TARGET"
else
    echo "  NestedPkg.pkg: FAIL - $SYMLINK_NAME is not a symbolic link"
    exit 1
fi

echo ""
echo "Done. Updated NestedPkg.pkg and test.pkg with symbolic link."
