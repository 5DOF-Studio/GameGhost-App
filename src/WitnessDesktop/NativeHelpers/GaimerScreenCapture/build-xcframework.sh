#!/bin/bash
# build-xcframework.sh
# Builds GaimerScreenCapture as an xcframework for Mac Catalyst consumption.
#
# Uses xcodebuild to compile the Swift Package for Mac Catalyst,
# then wraps the output in an xcframework and copies it to the MAUI project.
#
# Prerequisites: Xcode with Mac Catalyst support, Swift 5.9+
# Usage: ./build-xcframework.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/.build-xcframework"
FRAMEWORK_NAME="GaimerScreenCapture"

echo "=== Building $FRAMEWORK_NAME xcframework ==="
echo "Script dir: $SCRIPT_DIR"
echo "Build dir:  $BUILD_DIR"

# 1. Clean previous build artifacts
echo ""
echo "--- Cleaning previous builds ---"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# 2. Build the Swift Package for Mac Catalyst using xcodebuild
echo ""
echo "--- Building for Mac Catalyst (Release) ---"
xcodebuild build \
    -scheme "$FRAMEWORK_NAME" \
    -destination "generic/platform=macOS,variant=Mac Catalyst" \
    -derivedDataPath "$BUILD_DIR/derived" \
    -configuration Release \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
    SUPPORTS_MACCATALYST=YES \
    2>&1 | tail -20

echo ""
echo "--- Locating built framework ---"

# 3. Locate the built framework or dylib in derived data
BUILT_FRAMEWORK=$(find "$BUILD_DIR/derived" -name "${FRAMEWORK_NAME}.framework" -type d | head -1)

if [ -z "$BUILT_FRAMEWORK" ]; then
    echo "Framework directory not found. Looking for bare dylib..."

    # If xcodebuild produces a bare dylib instead of a .framework, create the framework structure
    BUILT_DYLIB=$(find "$BUILD_DIR/derived" -name "lib${FRAMEWORK_NAME}.dylib" -o -name "${FRAMEWORK_NAME}" -type f | grep -v ".dSYM" | head -1)

    if [ -z "$BUILT_DYLIB" ]; then
        # Check for .o or other artifacts
        echo "Build artifacts:"
        find "$BUILD_DIR/derived" -name "*${FRAMEWORK_NAME}*" -not -path "*/SourcePackages/*" 2>/dev/null || true
        echo ""
        echo "ERROR: Could not find built framework or dylib."
        echo "Check xcodebuild output above for errors."
        exit 1
    fi

    echo "Found dylib: $BUILT_DYLIB"

    # Create framework structure manually
    BUILT_FRAMEWORK="$BUILD_DIR/${FRAMEWORK_NAME}.framework"
    mkdir -p "$BUILT_FRAMEWORK"
    cp "$BUILT_DYLIB" "$BUILT_FRAMEWORK/${FRAMEWORK_NAME}"

    # Create minimal Info.plist
    cat > "$BUILT_FRAMEWORK/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleIdentifier</key>
    <string>com.5dof.gaimer.screencapture</string>
    <key>CFBundleName</key>
    <string>${FRAMEWORK_NAME}</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
    <key>CFBundleExecutable</key>
    <string>${FRAMEWORK_NAME}</string>
    <key>MinimumOSVersion</key>
    <string>16.0</string>
    <key>CFBundleSupportedPlatforms</key>
    <array>
        <string>MacOSX</string>
    </array>
</dict>
</plist>
PLIST

    echo "Created framework structure at: $BUILT_FRAMEWORK"
fi

echo "Using framework: $BUILT_FRAMEWORK"

# Verify the binary exists and check architecture
BINARY_PATH="$BUILT_FRAMEWORK/${FRAMEWORK_NAME}"
if [ -f "$BINARY_PATH" ]; then
    echo "Binary architecture:"
    file "$BINARY_PATH"
    echo ""
fi

# 4. Create the xcframework
echo "--- Creating xcframework ---"
XCFRAMEWORK_PATH="$BUILD_DIR/${FRAMEWORK_NAME}.xcframework"
rm -rf "$XCFRAMEWORK_PATH"

xcodebuild -create-xcframework \
    -framework "$BUILT_FRAMEWORK" \
    -output "$XCFRAMEWORK_PATH" \
    2>&1

if [ ! -d "$XCFRAMEWORK_PATH" ]; then
    echo "ERROR: Failed to create xcframework."
    exit 1
fi

echo "Created: $XCFRAMEWORK_PATH"

# 5. Copy the xcframework to the MAUI project
echo ""
echo "--- Copying to MAUI project ---"
DEST="$SCRIPT_DIR/../../WitnessDesktop/Platforms/MacCatalyst"
mkdir -p "$DEST"
rm -rf "$DEST/${FRAMEWORK_NAME}.xcframework"
cp -R "$XCFRAMEWORK_PATH" "$DEST/"

echo ""
echo "=== SUCCESS ==="
echo "xcframework built and copied to:"
echo "  $DEST/${FRAMEWORK_NAME}.xcframework"
echo ""
echo "Binary info:"
find "$DEST/${FRAMEWORK_NAME}.xcframework" -name "${FRAMEWORK_NAME}" -not -name "*.plist" -type f -exec file {} \;
