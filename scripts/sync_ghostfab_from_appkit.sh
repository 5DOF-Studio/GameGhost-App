#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DEFAULT_SOURCE_XCFRAMEWORK="/Users/tonynlemadim/Documents/5DOF Projects/ghostFab-appkit/dist/GaimerGhostMode.xcframework"
SOURCE_XCFRAMEWORK="${1:-$DEFAULT_SOURCE_XCFRAMEWORK}"
DEST_XCFRAMEWORK="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/GaimerGhostMode.xcframework"

if [[ ! -d "$SOURCE_XCFRAMEWORK" ]]; then
  echo "Source xcframework not found: $SOURCE_XCFRAMEWORK" >&2
  exit 1
fi

if [[ ! -f "$SOURCE_XCFRAMEWORK/Info.plist" ]]; then
  echo "Source xcframework is missing Info.plist: $SOURCE_XCFRAMEWORK" >&2
  exit 1
fi

SOURCE_FRAMEWORK=""
if [[ -d "$SOURCE_XCFRAMEWORK/ios-arm64_x86_64-maccatalyst/GaimerGhostMode.framework" ]]; then
  SOURCE_FRAMEWORK="$SOURCE_XCFRAMEWORK/ios-arm64_x86_64-maccatalyst/GaimerGhostMode.framework"
elif [[ -d "$SOURCE_XCFRAMEWORK/macos-arm64_x86_64/GaimerGhostMode.framework" ]]; then
  SOURCE_FRAMEWORK="$SOURCE_XCFRAMEWORK/macos-arm64_x86_64/GaimerGhostMode.framework"
fi

if [[ -z "$SOURCE_FRAMEWORK" ]]; then
  echo "No supported GaimerGhostMode.framework slice found under: $SOURCE_XCFRAMEWORK" >&2
  exit 1
fi

echo "Replacing mounted GhostFab xcframework"
echo "  source: $SOURCE_XCFRAMEWORK"
echo "  dest:   $DEST_XCFRAMEWORK"

rm -rf "$DEST_XCFRAMEWORK"
mkdir -p "$(dirname "$DEST_XCFRAMEWORK")"
cp -R "$SOURCE_XCFRAMEWORK" "$DEST_XCFRAMEWORK"

if [[ ! -d "$DEST_XCFRAMEWORK" ]]; then
  echo "Mounted xcframework was not created: $DEST_XCFRAMEWORK" >&2
  exit 1
fi

echo
echo "Mounted successfully."
echo "Next recommended steps:"
echo "  1. Remove stale build outputs under src/WitnessDesktop/WitnessDesktop/bin and obj"
echo "  2. Rebuild the Mac Catalyst app"
echo "  3. Validate ghost mode create/show/hide, callbacks, audio toggles, and VAD updates"
