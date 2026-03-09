---
phase: 03-screen-capture
plan: 01
subsystem: capture
tags: [swift, screencapturekit, xcframework, native-interop, maccatalyst, pinvoke]

# Dependency graph
requires:
  - phase: 02-chat-brain
    provides: "MAUI project structure with MacCatalyst target and existing NativeMethods.cs P/Invoke pattern"
provides:
  - "GaimerScreenCapture.xcframework with sck_is_available and sck_capture_window @_cdecl exports"
  - "Build script (build-xcframework.sh) for reproducible xcframework builds"
  - "NativeReference in GaimerDesktop.csproj linking xcframework for Mac Catalyst"
affects: [03-02-screen-capture-service, 03-03-screen-capture-integration]

# Tech tracking
tech-stack:
  added: [ScreenCaptureKit (via native Swift helper), ImageIO, UniformTypeIdentifiers]
  patterns: ["@_cdecl + @convention(c) callback for Swift-to-C# interop", "xcframework via xcodebuild from Swift Package", "ImageIO CGImageDestination for platform-portable PNG encoding"]

key-files:
  created:
    - src/GaimerDesktop/NativeHelpers/GaimerScreenCapture/Package.swift
    - src/GaimerDesktop/NativeHelpers/GaimerScreenCapture/Sources/GaimerScreenCapture/GaimerScreenCapture.swift
    - src/GaimerDesktop/NativeHelpers/GaimerScreenCapture/build-xcframework.sh
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/GaimerScreenCapture.xcframework/
  modified:
    - src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj
    - .gitignore

key-decisions:
  - "@_cdecl with C function pointer callback over @objc + ObjC block marshaling -- simpler P/Invoke"
  - "ImageIO CGImageDestination for PNG encoding instead of NSBitmapImageRep -- Mac Catalyst compatible"
  - "SCScreenshotManager single-frame capture over SCStream -- correct tool for 30-second intervals"
  - "Pre-built xcframework committed to repo -- avoids requiring Xcode for MAUI build"

patterns-established:
  - "Native Swift helper pattern: Swift Package -> xcodebuild -> xcframework -> NativeReference"
  - "@_cdecl exports with @convention(c) callbacks for async Swift-to-C# interop"
  - "Availability guards: if #available(macCatalyst 18.2, macOS 14.0, *)"

# Metrics
duration: 6min
completed: 2026-02-25
---

# Phase 03 Plan 01: Native SCK Helper Summary

**Swift xcframework wrapping ScreenCaptureKit via @_cdecl exports with ImageIO PNG encoding for P/Invoke from .NET MAUI Mac Catalyst**

## Performance

- **Duration:** 6 min
- **Started:** 2026-02-26T00:13:27Z
- **Completed:** 2026-02-26T00:19:41Z
- **Tasks:** 3
- **Files modified:** 6

## Accomplishments
- Created Swift Package with GaimerScreenCapture.swift exporting `sck_is_available` and `sck_capture_window` via @_cdecl
- Built universal xcframework (arm64 + x86_64) for Mac Catalyst using xcodebuild
- Linked xcframework into MAUI project via NativeReference -- build succeeds with 0 errors
- Used ImageIO CGImageDestination for CGImage-to-PNG conversion (Mac Catalyst compatible, not AppKit)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create Swift Package and native helper source** - `4b0046d` (feat)
2. **Task 2: Create build script and build xcframework** - `c681a20` (feat)
3. **Task 3: Link xcframework into MAUI project** - `ce20ac1` (feat)

## Files Created/Modified
- `src/GaimerDesktop/NativeHelpers/GaimerScreenCapture/Package.swift` - Swift Package manifest with Mac Catalyst + macOS platforms
- `src/GaimerDesktop/NativeHelpers/GaimerScreenCapture/Sources/GaimerScreenCapture/GaimerScreenCapture.swift` - SCK wrapper with @_cdecl exports, ImageIO PNG encoding, availability guards
- `src/GaimerDesktop/NativeHelpers/GaimerScreenCapture/build-xcframework.sh` - Build script using xcodebuild for Mac Catalyst xcframework
- `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/GaimerScreenCapture.xcframework/` - Compiled xcframework with universal binary
- `src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj` - Added NativeReference for Mac Catalyst
- `.gitignore` - Added .build-xcframework/ exclusion

## Decisions Made
- **@_cdecl over @objc:** Used @_cdecl with C function pointer callback instead of ObjC blocks. P/Invoke with function pointers is simpler and better understood in .NET than ObjC block marshaling.
- **ImageIO over NSBitmapImageRep:** Used CGImageDestination from ImageIO framework for PNG encoding. NSBitmapImageRep requires AppKit which is unavailable on Mac Catalyst.
- **SCScreenshotManager over SCStream:** Single-frame capture API is the correct tool for 30-second interval snapshots. SCStream is designed for continuous streaming and would waste resources.
- **Pre-built xcframework:** Committed the compiled xcframework to the repo so the MAUI project builds without requiring Xcode on the build machine.
- **BGRA pixel format:** Explicitly set kCVPixelFormatType_32BGRA in SCStreamConfiguration because SkiaSharp expects BGRA, not the default YUV format.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - xcodebuild resolved the Swift Package scheme correctly, the framework built for both architectures, and the MAUI project linked it without issues.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- xcframework is linked and MAUI builds successfully
- Next plan (03-02) should create the C# P/Invoke declarations and WindowCaptureService integration calling sck_is_available and sck_capture_window
- The @_cdecl pattern means C# needs: `[DllImport("GaimerScreenCapture")]` with matching function signatures and a callback delegate
- Screen Recording permission (existing CGPreflightScreenCaptureAccess) also covers SCK -- no new entitlements needed

---
*Phase: 03-screen-capture*
*Completed: 2026-02-25*
