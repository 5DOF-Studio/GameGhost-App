#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

SOURCE_XCFRAMEWORK="${1:-/Users/tonynlemadim/Documents/5DOF Projects/ghostFab-appkit/dist/GaimerGhostMode.xcframework}"
PROJECT_PATH="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj"

echo "Refreshing GhostFab mount from source artifact"
echo "  source: $SOURCE_XCFRAMEWORK"
echo

zsh "$SCRIPT_DIR/sync_ghostfab_from_appkit.sh" "$SOURCE_XCFRAMEWORK"
echo

echo "Verifying mounted xcframework compatibility"
zsh "$SCRIPT_DIR/verify_ghostfab_compat.sh"
echo

echo "Cleaning stale Mac Catalyst build outputs"
dotnet clean "$PROJECT_PATH" -f net8.0-maccatalyst >/dev/null
rm -rf "$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/bin" || true
rm -rf "$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/obj" || true
echo

echo "Rebuilding Gaimer for Mac Catalyst"
dotnet build "$PROJECT_PATH" -f net8.0-maccatalyst -p:EnableCodeSigning=false
echo

echo "Verifying GhostFab framework inside built app bundle"
zsh "$SCRIPT_DIR/verify_ghostfab_app_bundle.sh"
echo

echo "GhostFab refresh flow completed successfully."
