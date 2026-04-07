#!/bin/bash
# build-all-natives.sh
# Rebuilds all 3 native xcframeworks from Swift source and copies them
# to the MAUI project's Platforms/MacCatalyst/ directory.
#
# Prerequisites: Xcode with Mac Catalyst support, Swift 5.9+
# Usage: ./scripts/build-all-natives.sh
#
# The built xcframeworks are tracked in git so that cloning the repo
# is sufficient to build the .NET project. Run this script only when
# the Swift source has changed or you need to regenerate the binaries
# (new Xcode version, architecture changes, etc.).

set -e

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NATIVE_DIR="$REPO_ROOT/src/WitnessDesktop/NativeHelpers"

FRAMEWORKS=("GaimerScreenCapture" "GaimerSpeech" "GaimerGhostMode")
FAILED=()

for FW in "${FRAMEWORKS[@]}"; do
    echo ""
    echo "========================================"
    echo " Building $FW"
    echo "========================================"
    if bash "$NATIVE_DIR/$FW/build-xcframework.sh"; then
        echo "  $FW: OK"
    else
        echo "  $FW: FAILED"
        FAILED+=("$FW")
    fi
done

echo ""
echo "========================================"
echo " Summary"
echo "========================================"

DEST="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst"
for FW in "${FRAMEWORKS[@]}"; do
    if [ -d "$DEST/$FW.xcframework" ]; then
        SIZE=$(du -sh "$DEST/$FW.xcframework" | cut -f1)
        echo "  $FW.xcframework  $SIZE"
    else
        echo "  $FW.xcframework  MISSING"
    fi
done

if [ ${#FAILED[@]} -gt 0 ]; then
    echo ""
    echo "FAILED: ${FAILED[*]}"
    exit 1
fi

echo ""
echo "All native frameworks built successfully."
echo "To commit updated binaries: git add -f src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/*.xcframework"
