#!/bin/bash
# build-xcframework.sh
# Builds GaimerGhostMode as an xcframework for consumption from .NET MAUI (Mac Catalyst).
#
# Uses xcodebuild to compile the Swift Package for macOS (NOT Mac Catalyst),
# then wraps the output in an xcframework and copies it to the MAUI project.
#
# Key difference from GaimerScreenCapture: This targets macOS (not Mac Catalyst)
# because it needs import AppKit for NSPanel.
#
# Prerequisites: Xcode with macOS SDK, Swift 5.9+
# Usage: ./build-xcframework.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="$SCRIPT_DIR/.build-xcframework"
FRAMEWORK_NAME="GaimerGhostMode"

echo "=== Building $FRAMEWORK_NAME xcframework ==="
echo "Script dir: $SCRIPT_DIR"
echo "Build dir:  $BUILD_DIR"

# 1. Clean previous build artifacts
echo ""
echo "--- Cleaning previous builds ---"
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# 2. Build the Swift Package for macOS using xcodebuild
#    We build for plain macOS (not Mac Catalyst) because the code imports AppKit
#    (NSPanel, NSWindow) which isn't available in the Catalyst SDK headers.
#    After building, we use vtool to retag the binary as Mac Catalyst so that
#    the Catalyst runtime's dlopen accepts it. AppKit is available at runtime
#    in Catalyst processes — only the platform tag in the Mach-O header matters.
echo ""
echo "--- Building for macOS (Release) ---"
xcodebuild build \
    -scheme "$FRAMEWORK_NAME" \
    -destination "generic/platform=macOS" \
    -derivedDataPath "$BUILD_DIR/derived" \
    -configuration Release \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES \
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
    <string>com.5dof.gaimer.ghostmode</string>
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
    <string>13.0</string>
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

# 4. Create the xcframework (still tagged as macOS at this point)
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

# 6. Retag the final binary from macOS (platform 1) to Mac Catalyst (platform 6)
#    so the Catalyst runtime's dlopen will accept it.
#    Must be done AFTER xcodebuild -create-xcframework because xcodebuild copies
#    fresh binaries and would revert any earlier retagging.
echo ""
echo "--- Retagging binary for Mac Catalyst ---"
FINAL_BINARY=$(find "$DEST/${FRAMEWORK_NAME}.xcframework" -name "${FRAMEWORK_NAME}" -not -name "*.plist" -type f | head -1)

if [ -n "$FINAL_BINARY" ] && [ -f "$FINAL_BINARY" ]; then
    RETAG_DIR="$BUILD_DIR/retag"
    rm -rf "$RETAG_DIR"
    mkdir -p "$RETAG_DIR"

    for ARCH in arm64 x86_64; do
        lipo "$FINAL_BINARY" -thin "$ARCH" -output "$RETAG_DIR/${FRAMEWORK_NAME}_${ARCH}" 2>/dev/null || continue
        vtool -set-build-version maccatalyst 16.0 26.2 \
            -replace \
            -output "$RETAG_DIR/${FRAMEWORK_NAME}_${ARCH}_retagged" \
            "$RETAG_DIR/${FRAMEWORK_NAME}_${ARCH}"
        mv "$RETAG_DIR/${FRAMEWORK_NAME}_${ARCH}_retagged" "$RETAG_DIR/${FRAMEWORK_NAME}_${ARCH}"
    done

    lipo -create "$RETAG_DIR/${FRAMEWORK_NAME}_arm64" "$RETAG_DIR/${FRAMEWORK_NAME}_x86_64" \
        -output "$FINAL_BINARY"
    rm -rf "$RETAG_DIR"

    echo "Binary retagged. Verifying platform:"
    vtool -show-build "$FINAL_BINARY" | grep -A2 "platform"
else
    echo "WARNING: Could not find binary to retag at $DEST"
fi

echo ""
echo "=== SUCCESS ==="
echo "xcframework built and copied to:"
echo "  $DEST/${FRAMEWORK_NAME}.xcframework"
echo ""
echo "Binary info:"
find "$DEST/${FRAMEWORK_NAME}.xcframework" -name "${FRAMEWORK_NAME}" -not -name "*.plist" -type f -exec file {} \;
