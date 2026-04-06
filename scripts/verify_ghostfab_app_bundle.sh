#!/bin/zsh

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

DEFAULT_APP_BUNDLE="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/Gaimer.app"
APP_BUNDLE_PATH="${1:-$DEFAULT_APP_BUNDLE}"
FRAMEWORK_DIR="$APP_BUNDLE_PATH/Contents/Frameworks/GaimerGhostMode.framework"

if [[ ! -d "$APP_BUNDLE_PATH" ]]; then
  echo "App bundle not found: $APP_BUNDLE_PATH" >&2
  exit 1
fi

if [[ ! -d "$FRAMEWORK_DIR" ]]; then
  echo "GaimerGhostMode.framework not found in app bundle: $FRAMEWORK_DIR" >&2
  exit 1
fi

BINARY_PATH=""
if [[ -f "$FRAMEWORK_DIR/GaimerGhostMode" ]]; then
  BINARY_PATH="$FRAMEWORK_DIR/GaimerGhostMode"
elif [[ -f "$FRAMEWORK_DIR/Versions/A/GaimerGhostMode" ]]; then
  BINARY_PATH="$FRAMEWORK_DIR/Versions/A/GaimerGhostMode"
fi

if [[ -z "$BINARY_PATH" ]]; then
  echo "GaimerGhostMode binary not found in app bundle framework: $FRAMEWORK_DIR" >&2
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

echo "Verifying GhostFab framework in built app bundle"
echo "  app bundle: $APP_BUNDLE_PATH"
echo "  framework:  $FRAMEWORK_DIR"
echo "  binary:     $BINARY_PATH"

for export_name in "${required_exports[@]}"; do
  if ! nm -gU "$BINARY_PATH" | rg -q "_${export_name}$"; then
    echo "Missing required export in app bundle binary: $export_name" >&2
    exit 1
  fi
done

echo
echo "App bundle compatibility check passed."
