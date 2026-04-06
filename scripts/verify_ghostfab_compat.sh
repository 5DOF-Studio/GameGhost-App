#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DEFAULT_XCFRAMEWORK="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/Platforms/MacCatalyst/GaimerGhostMode.xcframework"
XCFRAMEWORK_PATH="${1:-$DEFAULT_XCFRAMEWORK}"

if [[ ! -d "$XCFRAMEWORK_PATH" ]]; then
  echo "GhostFab xcframework not found: $XCFRAMEWORK_PATH" >&2
  exit 1
fi

if [[ ! -f "$XCFRAMEWORK_PATH/Info.plist" ]]; then
  echo "GhostFab xcframework is missing Info.plist: $XCFRAMEWORK_PATH" >&2
  exit 1
fi

FRAMEWORK_PATH=""
if [[ -d "$XCFRAMEWORK_PATH/ios-arm64_x86_64-maccatalyst/GaimerGhostMode.framework" ]]; then
  FRAMEWORK_PATH="$XCFRAMEWORK_PATH/ios-arm64_x86_64-maccatalyst/GaimerGhostMode.framework"
elif [[ -d "$XCFRAMEWORK_PATH/macos-arm64_x86_64/GaimerGhostMode.framework" ]]; then
  FRAMEWORK_PATH="$XCFRAMEWORK_PATH/macos-arm64_x86_64/GaimerGhostMode.framework"
fi

if [[ -z "$FRAMEWORK_PATH" ]]; then
  echo "No supported GaimerGhostMode.framework slice found under: $XCFRAMEWORK_PATH" >&2
  exit 1
fi

BINARY_PATH=""
if [[ -f "$FRAMEWORK_PATH/GaimerGhostMode" ]]; then
  BINARY_PATH="$FRAMEWORK_PATH/GaimerGhostMode"
elif [[ -f "$FRAMEWORK_PATH/Versions/A/GaimerGhostMode" ]]; then
  BINARY_PATH="$FRAMEWORK_PATH/Versions/A/GaimerGhostMode"
fi

if [[ -z "$BINARY_PATH" ]]; then
  echo "GaimerGhostMode binary not found under framework: $FRAMEWORK_PATH" >&2
  exit 1
fi

required_exports=(
  ghost_panel_create
  ghost_panel_destroy
  ghost_panel_show
  ghost_panel_hide
  ghost_panel_hide_host_window
  ghost_panel_show_host_window
  ghost_panel_show_card
  ghost_panel_dismiss_card
  ghost_panel_set_agent_image
  ghost_panel_set_fab_active
  ghost_panel_set_fab_connected
  ghost_panel_set_position
  ghost_panel_set_size
  ghost_panel_set_audio_state
  ghost_panel_set_vad_level
  ghost_panel_set_fab_tap_callback
  ghost_panel_set_card_dismiss_callback
  ghost_panel_set_gear_tap_callback
  ghost_panel_set_audio_toggle_callback
)

echo "Verifying GhostFab compatibility artifact"
echo "  xcframework: $XCFRAMEWORK_PATH"
echo "  framework:   $FRAMEWORK_PATH"
echo "  binary:      $BINARY_PATH"

for export_name in "${required_exports[@]}"; do
  if ! nm -gU "$BINARY_PATH" | rg -q "_${export_name}$"; then
    echo "Missing required export: $export_name" >&2
    exit 1
  fi
done

echo
echo "Compatibility check passed."
echo "Verified exports:"
for export_name in "${required_exports[@]}"; do
  echo "  - $export_name"
done
