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
#   ./scripts/build-release-mac.sh                    (full notarized release)
#   ./scripts/build-release-mac.sh --skip-notarize    (signed but not notarized)
#   ./scripts/build-release-mac.sh --local-deploy     (sign + deploy to /Applications + launch)
#

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
PROJECT_DIR="$REPO_ROOT/src/WitnessDesktop/WitnessDesktop"
PROJECT_FILE="$PROJECT_DIR/WitnessDesktop.csproj"
ENTITLEMENTS="$SCRIPT_DIR/WitnessDesktop.entitlements"
# Output MUST be outside ~/Documents to avoid File Provider extended attributes
# that block codesign ("resource fork, Finder information, or similar detritus")
OUTPUT_DIR="/tmp/gaimer-dist"

# Configuration — APP_NAME must match <AssemblyName> in csproj
APP_NAME="Gaimer"
FRAMEWORK="net8.0-maccatalyst"
CONFIGURATION="Release"
CREDENTIAL_PROFILE="GaimerNotary"
ZIP_STAGING_APP="$OUTPUT_DIR/${APP_NAME}-zip-staging.app"

SKIP_NOTARIZE=false
LOCAL_DEPLOY=false
for arg in "$@"; do
    case "$arg" in
        --skip-notarize) SKIP_NOTARIZE=true ;;
        --local-deploy)  LOCAL_DEPLOY=true; SKIP_NOTARIZE=true ;;
    esac
done

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
dotnet publish "$PROJECT_FILE" \
    -f "$FRAMEWORK" \
    -c "$CONFIGURATION" \
    -p:EnableCodeSigning=false \
    -p:MtouchLink=SdkOnly

# MacCatalyst publish puts .app in the RID directory (not under publish/)
# The publish/ folder only contains a .pkg — we want the .app bundle
# Pick the bundle matching the host architecture
if [[ "$(uname -m)" == "arm64" ]]; then
    HOST_RID="maccatalyst-arm64"
    ALT_RID="maccatalyst-x64"
else
    HOST_RID="maccatalyst-x64"
    ALT_RID="maccatalyst-arm64"
fi

APP_BUNDLE="$PROJECT_DIR/bin/$CONFIGURATION/$FRAMEWORK/$HOST_RID/$APP_NAME.app"

if [[ ! -d "$APP_BUNDLE" ]]; then
    # Fallback: try other architecture
    APP_BUNDLE="$PROJECT_DIR/bin/$CONFIGURATION/$FRAMEWORK/$ALT_RID/$APP_NAME.app"
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

# Fix app icon: dotnet publish produces a broken Assets.car on MacCatalyst that lacks
# icon renditions. Always overwrite with the debug build's Assets.car and appicon.icns.
echo "==> Recovering app icon from debug build (publish strips icon from Assets.car)..."
DEBUG_RES="$PROJECT_DIR/bin/Debug/$FRAMEWORK/$HOST_RID/$APP_NAME.app/Contents/Resources"
if [[ ! -d "$DEBUG_RES" ]]; then
    DEBUG_RES="$PROJECT_DIR/bin/Debug/$FRAMEWORK/$ALT_RID/$APP_NAME.app/Contents/Resources"
fi
if [[ -f "$DEBUG_RES/Assets.car" ]]; then
    cp "$DEBUG_RES/Assets.car" "$DIST_APP/Contents/Resources/Assets.car"
    echo "    Copied Assets.car"
else
    echo "WARNING: No Assets.car in debug builds. Run 'dotnet build -f $FRAMEWORK' first."
fi
if [[ -f "$DEBUG_RES/appicon.icns" ]]; then
    cp "$DEBUG_RES/appicon.icns" "$DIST_APP/Contents/Resources/appicon.icns"
    echo "    Copied appicon.icns"
fi
# Ensure Info.plist references the icon
if ! /usr/libexec/PlistBuddy -c "Print :CFBundleIconFile" "$DIST_APP/Contents/Info.plist" &>/dev/null; then
    /usr/libexec/PlistBuddy -c "Add :CFBundleIconFile string appicon" "$DIST_APP/Contents/Info.plist"
    /usr/libexec/PlistBuddy -c "Add :CFBundleIconName string appicon" "$DIST_APP/Contents/Info.plist"
    echo "    Patched Info.plist icon entries"
fi

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
rm -rf "$ZIP_STAGING_APP"
ditto --norsrc "$DIST_APP" "$ZIP_STAGING_APP"
ditto -c -k --sequesterRsrc --keepParent "$ZIP_STAGING_APP" "$SUBMIT_ZIP"
rm -rf "$ZIP_STAGING_APP"
echo "==> Zip: $SUBMIT_ZIP ($(du -h "$SUBMIT_ZIP" | awk '{print $1}'))"

if $SKIP_NOTARIZE; then
    echo ""
    echo "==> Skipping notarization (--skip-notarize flag)"

    if $LOCAL_DEPLOY; then
        echo ""
        echo "==> Deploying to /Applications for local testing..."
        rm -rf /Applications/$APP_NAME.app
        ditto --norsrc "$DIST_APP" /Applications/$APP_NAME.app
        echo "==> Verifying /Applications/$APP_NAME.app..."
        codesign --verify --deep --strict /Applications/$APP_NAME.app
        echo "==> Launching..."
        open /Applications/$APP_NAME.app
    fi

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
rm -rf "$ZIP_STAGING_APP"
ditto --norsrc "$DIST_APP" "$ZIP_STAGING_APP"
ditto -c -k --sequesterRsrc --keepParent "$ZIP_STAGING_APP" "$FINAL_ZIP"
rm -rf "$ZIP_STAGING_APP"

echo ""
echo "==> Build complete!"
echo "    Signed app:     $DIST_APP"
echo "    Distribution:   $FINAL_ZIP ($(du -h "$FINAL_ZIP" | awk '{print $1}'))"
echo ""
echo "    Users can unzip and drag to /Applications."
