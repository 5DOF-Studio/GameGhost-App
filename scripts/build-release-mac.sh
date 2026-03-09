#!/usr/bin/env bash
set -euo pipefail

#
# GAIMER Desktop — macOS Release Build + Notarize
#
# Prerequisites:
#   1. Developer ID Application certificate installed in Keychain
#   2. Notarization credentials stored via: xcrun notarytool store-credentials "GaimerNotary"
#   3. .NET 8.0 SDK + MAUI workloads installed
#
# Usage:
#   ./scripts/build-release-mac.sh
#   ./scripts/build-release-mac.sh --skip-notarize   (for local testing)
#

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop"
ENTITLEMENTS="$SCRIPT_DIR/WitnessDesktop.entitlements"
# Output MUST be outside ~/Documents to avoid File Provider extended attributes
# that block codesign ("resource fork, Finder information, or similar detritus")
OUTPUT_DIR="/tmp/gaimer-dist"

# Configuration
APP_NAME="WitnessDesktop"
FRAMEWORK="net8.0-maccatalyst"
CONFIGURATION="Release"
CREDENTIAL_PROFILE="GaimerNotary"

SKIP_NOTARIZE=false
if [[ "${1:-}" == "--skip-notarize" ]]; then
    SKIP_NOTARIZE=true
fi

# Find Developer ID certificate
SIGNING_IDENTITY=$(security find-identity -v -p codesigning | grep "Developer ID Application" | head -1 | awk -F'"' '{print $2}')
if [[ -z "$SIGNING_IDENTITY" ]]; then
    echo "ERROR: No Developer ID Application certificate found in Keychain."
    echo "Run: security find-identity -v -p codesigning"
    exit 1
fi
echo "==> Signing identity: $SIGNING_IDENTITY"

# Verify entitlements exist
if [[ ! -f "$ENTITLEMENTS" ]]; then
    echo "ERROR: Entitlements file not found at $ENTITLEMENTS"
    exit 1
fi

# Clean output
rm -rf "$OUTPUT_DIR"
mkdir -p "$OUTPUT_DIR"

# Step 1: Publish
echo ""
echo "==> Step 1: Publishing $APP_NAME ($CONFIGURATION)..."
dotnet publish "$PROJECT_DIR/$APP_NAME.csproj" \
    -f "$FRAMEWORK" \
    -c "$CONFIGURATION" \
    -p:EnableCodeSigning=false \
    -p:MtouchLink=SdkOnly

# MacCatalyst publish puts .app in the RID directory (not under publish/)
# The publish/ folder only contains a .pkg — we want the .app bundle
APP_BUNDLE="$PROJECT_DIR/bin/$CONFIGURATION/$FRAMEWORK/maccatalyst-arm64/$APP_NAME.app"

if [[ ! -d "$APP_BUNDLE" ]]; then
    # Fallback: try x64
    APP_BUNDLE="$PROJECT_DIR/bin/$CONFIGURATION/$FRAMEWORK/maccatalyst-x64/$APP_NAME.app"
fi

if [[ ! -d "$APP_BUNDLE" ]]; then
    echo "ERROR: Published .app bundle not found. Checked:"
    echo "  $PROJECT_DIR/bin/$CONFIGURATION/$FRAMEWORK/maccatalyst-arm64/"
    echo "  $PROJECT_DIR/bin/$CONFIGURATION/$FRAMEWORK/maccatalyst-x64/"
    exit 1
fi
echo "==> App bundle: $APP_BUNDLE"

# Step 2: Copy to output
echo ""
echo "==> Step 2: Copying to dist/..."
DIST_APP="$OUTPUT_DIR/$APP_NAME.app"
ditto --norsrc "$APP_BUNDLE" "$DIST_APP"

# Strip extended attributes (File Provider, Finder metadata) that block codesign
xattr -cr "$DIST_APP"

# Step 3: Codesign with hardened runtime
echo ""
echo "==> Step 3: Signing with Developer ID (hardened runtime)..."
codesign --force --deep --options runtime \
    --sign "$SIGNING_IDENTITY" \
    --entitlements "$ENTITLEMENTS" \
    "$DIST_APP"

echo "==> Verifying signature..."
codesign --verify --deep --strict "$DIST_APP"
spctl --assess --type exec "$DIST_APP" 2>&1 || echo "  (spctl may fail before notarization — expected)"

# Step 4: Create zip for notarization
echo ""
echo "==> Step 4: Creating zip for notarization..."
SUBMIT_ZIP="$OUTPUT_DIR/$APP_NAME.zip"
ditto -c -k --sequesterRsrc --keepParent "$DIST_APP" "$SUBMIT_ZIP"
echo "==> Zip: $SUBMIT_ZIP ($(du -h "$SUBMIT_ZIP" | awk '{print $1}'))"

if $SKIP_NOTARIZE; then
    echo ""
    echo "==> Skipping notarization (--skip-notarize flag)"
    echo "==> Done! Output: $OUTPUT_DIR/"
    ls -lh "$OUTPUT_DIR/"
    exit 0
fi

# Step 5: Notarize
echo ""
echo "==> Step 5: Submitting for notarization (this may take a few minutes)..."
xcrun notarytool submit "$SUBMIT_ZIP" \
    --keychain-profile "$CREDENTIAL_PROFILE" \
    --wait

# Step 6: Staple
echo ""
echo "==> Step 6: Stapling notarization ticket..."
xcrun stapler staple "$DIST_APP"

# Step 7: Re-zip with stapled ticket
echo ""
echo "==> Step 7: Creating final distribution zip..."
FINAL_ZIP="$OUTPUT_DIR/${APP_NAME}-notarized.zip"
ditto -c -k --sequesterRsrc --keepParent "$DIST_APP" "$FINAL_ZIP"

echo ""
echo "==> Build complete!"
echo "    Signed app:     $DIST_APP"
echo "    Distribution:   $FINAL_ZIP ($(du -h "$FINAL_ZIP" | awk '{print $1}'))"
echo ""
echo "    Users can unzip and drag to /Applications."
