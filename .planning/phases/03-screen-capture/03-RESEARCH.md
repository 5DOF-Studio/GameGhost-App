# Phase 03: Screen Capture - ScreenCaptureKit Migration - Research

**Researched:** 2026-02-25
**Domain:** macOS ScreenCaptureKit integration via .NET MAUI MacCatalyst
**Confidence:** MEDIUM (verified architecture approach; implementation details require validation)

## Summary

The current screen capture implementation uses `CGDisplayCreateImage` (a display-level CoreGraphics API) which cannot capture GPU/Metal-rendered content. Apple's Chess.app (SceneKit/Metal) and all modern games appear transparent in captures. The fix is migrating to ScreenCaptureKit (SCK), Apple's modern screen capture framework that captures all content including GPU-accelerated layers.

The critical finding is that **ScreenCaptureKit has NO managed C# bindings in the .NET 8 MacCatalyst SDK**. The `net8.0_18.0` reference assembly (`Microsoft.MacCatalyst.dll`) was inspected directly -- it contains 16,414 types across 131 namespaces but zero ScreenCaptureKit types. SCK bindings were added to the xamarin-macios project for `macCatalyst(18.2)` availability, but this landed in Xcode 16.2 beta 2 and is only available in .NET 9+ or .NET 10 preview SDKs, not .NET 8.

Additionally, ScreenCaptureKit on Mac Catalyst has a troubled history: it crashed on Mac Catalyst apps in early macOS versions, and official Mac Catalyst availability was only annotated starting with `macCatalyst(18.2)` in Xcode 16.2. The project targets `net8.0-maccatalyst` with `SupportedOSPlatformVersion` of `13.1`.

The recommended approach is to create a **native Swift helper library** that wraps SCK and exposes a simple Objective-C-compatible interface, then consume it from .NET via the Native Library Interop pattern (or simpler: a compiled xcframework with `@objc`-annotated Swift classes called via ObjCRuntime).

**Primary recommendation:** Build a thin Swift xcframework (`ScreenCaptureHelper`) that wraps SCScreenshotManager for single-frame captures (macOS 14+) and exposes `captureWindow(windowID:) -> Data?` via `@objc`. Link it into the MAUI project and call it via `ObjCRuntime.Runtime` messaging or `DllImport` against the framework. Keep the existing `CGDisplayCreateImage` path as fallback for macOS < 14.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ScreenCaptureKit | macOS 12.3+ (SCScreenshotManager: macOS 14+) | GPU-inclusive screen capture | Apple's official replacement for CGWindowListCreateImage; captures Metal/OpenGL/SceneKit content |
| SkiaSharp | 3.119.1 (already in project) | Image processing (decode, crop, encode) | Already used in project for PNG encode/decode |
| ObjCRuntime | Part of .NET MAUI MacCatalyst SDK | Objective-C interop from C# | Built into the MAUI platform; already used in project for NSDictionary/NSArray access |
| CoreMedia (CMSampleBuffer) | Part of .NET 8 MacCatalyst SDK | Media sample buffer handling | Managed bindings confirmed present in net8.0_18.0 reference assembly |
| CoreVideo (CVPixelBuffer) | Part of .NET 8 MacCatalyst SDK | Pixel buffer access | Managed bindings confirmed present; needed for image data extraction |
| CoreGraphics (CGImage) | Part of .NET 8 MacCatalyst SDK | Image representation | Managed bindings confirmed present; bridges to SkiaSharp pipeline |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Native Swift Helper (custom) | N/A | Wraps SCK API for .NET consumption | Required because SCK has no managed bindings in .NET 8 MacCatalyst |
| CGWindowListCopyWindowInfo | CoreGraphics (already P/Invoked) | Window enumeration | Keep for window listing -- works fine, already implemented |
| CGDisplayCreateImage | CoreGraphics (already P/Invoked) | Display-level capture (fallback) | Fallback for macOS < 14 or when SCK is unavailable |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Native Swift Helper | Raw ObjC interop via objc_msgSend | More brittle, requires hand-crafting every message send; async completion handlers are extremely difficult to marshal correctly via raw interop |
| Native Swift Helper | .NET 9 upgrade for managed SCK bindings | Would require upgrading the entire project from net8.0 to net9.0+; SCK Mac Catalyst bindings may still be incomplete in .NET 9 |
| SCScreenshotManager (single-frame) | SCStream (continuous streaming) | SCStream is overkill for 30-second interval captures; adds complexity of delegate management, stream lifecycle, memory pressure from continuous delivery |
| SCScreenshotManager | SCStream with immediate stop | Possible but unnecessarily complex; SCScreenshotManager was designed specifically for single-frame capture |

**Installation:**
No NuGet packages needed. The native helper is built as an xcframework and linked via `NativeReference` in the `.csproj`.

## Architecture Patterns

### Recommended Project Structure

```
src/GaimerDesktop/GaimerDesktop/
  Platforms/MacCatalyst/
    NativeMethods.cs              # Existing P/Invoke (keep CGWindowList*, add SCK helper imports)
    WindowCaptureService.cs       # Updated: delegates to SCK helper for frame capture
    ScreenCaptureHelper/          # NEW: Native Swift helper project
      ScreenCaptureHelper.swift   # SCK wrapper with @objc API
      ScreenCaptureHelper.h       # Bridging header for ObjC visibility
      Info.plist                  # Framework metadata
  Services/
    IWindowCaptureService.cs      # Interface unchanged
```

### Pattern 1: Native Swift Helper with @objc Interface

**What:** A small Swift framework that wraps ScreenCaptureKit and exposes an Objective-C-compatible API. The .NET side calls it via `ObjCRuntime` messaging or P/Invoke.

**When to use:** When Apple framework has no managed .NET bindings but needs to be called from .NET MAUI MacCatalyst.

**Why this pattern:** ScreenCaptureKit is Swift-native with async/await, completion handlers, and delegate patterns. These are extremely difficult to marshal correctly via raw `objc_msgSend` from C#. A Swift helper that does the async work internally and returns simple types (NSData, CGImage) is far more reliable.

**Swift side:**
```swift
// Source: Apple Developer Docs - SCScreenshotManager + Native Library Interop pattern
import Foundation
import ScreenCaptureKit
import CoreGraphics

@objc(GaimerScreenCapture)
public class GaimerScreenCapture: NSObject {

    /// Captures a single window by its CGWindowID and returns PNG data.
    /// Returns nil if capture fails or window not found.
    @objc public static func captureWindow(
        _ windowID: UInt32,
        width: Int,
        height: Int,
        completion: @escaping (NSData?) -> Void
    ) {
        Task {
            do {
                let content = try await SCShareableContent.excludingDesktopWindows(
                    false, onScreenWindowsOnly: true
                )
                guard let window = content.windows.first(where: { $0.windowID == windowID }) else {
                    completion(nil)
                    return
                }

                let filter = SCContentFilter(desktopIndependentWindow: window)
                let config = SCStreamConfiguration()
                config.width = width
                config.height = height
                config.pixelFormat = kCVPixelFormatType_32BGRA
                config.captureResolution = .best
                config.showsCursor = false

                let cgImage = try await SCScreenshotManager.captureImage(
                    contentFilter: filter, configuration: config
                )

                // Convert CGImage to PNG NSData
                let bitmapRep = NSBitmapImageRep(cgImage: cgImage)
                let pngData = bitmapRep.representation(
                    using: .png, properties: [:]
                )
                completion(pngData as NSData?)
            } catch {
                completion(nil)
            }
        }
    }

    /// Returns JSON-encoded array of visible windows with their IDs.
    /// Used for window enumeration alongside existing CGWindowList approach.
    @objc public static func getShareableWindows(
        completion: @escaping (NSArray?) -> Void
    ) {
        Task {
            do {
                let content = try await SCShareableContent.excludingDesktopWindows(
                    false, onScreenWindowsOnly: true
                )
                let windowInfos = NSMutableArray()
                for window in content.windows {
                    let info: NSDictionary = [
                        "windowID": window.windowID,
                        "title": window.title ?? "",
                        "owningApplicationName": window.owningApplication?.applicationName ?? "",
                        "owningApplicationBundleID": window.owningApplication?.bundleIdentifier ?? "",
                        "isOnScreen": window.isOnScreen,
                        "frame": [
                            "x": window.frame.origin.x,
                            "y": window.frame.origin.y,
                            "width": window.frame.size.width,
                            "height": window.frame.size.height
                        ]
                    ]
                    windowInfos.add(info)
                }
                completion(windowInfos)
            } catch {
                completion(nil)
            }
        }
    }
}
```

**C# side (calling the native helper):**
```csharp
// Source: ObjCRuntime.Messaging pattern from Xamarin/MAUI interop docs
using ObjCRuntime;
using Foundation;

// Get the class handle
var classHandle = Class.GetHandle("GaimerScreenCapture");

// Call captureWindow:width:height:completion:
var sel = Selector.GetHandle("captureWindow:width:height:completion:");

// Use TaskCompletionSource to bridge ObjC callback to C# async
var tcs = new TaskCompletionSource<NSData?>();
var callback = new Action<NSData?>(data => tcs.SetResult(data));
// ... marshal callback as ObjC block and invoke via objc_msgSend
```

### Pattern 2: Simpler Alternative -- @_cdecl with P/Invoke

**What:** Instead of ObjC messaging, export C-callable functions from Swift using `@_cdecl` and call them via `DllImport`.

**When to use:** When the ObjC messaging pattern is too complex for the async callback handling.

**Why it may be simpler:** P/Invoke with function pointers is better understood in .NET than ObjC block marshaling.

```swift
// Swift side with @_cdecl
@_cdecl("sck_capture_window")
public func sckCaptureWindow(
    windowID: UInt32,
    width: Int32,
    height: Int32,
    callback: @convention(c) (UnsafePointer<UInt8>?, Int32) -> Void
) {
    Task {
        // ... SCK capture logic ...
        // Call back with PNG bytes
        if let data = pngData {
            data.withUnsafeBytes { ptr in
                callback(ptr.bindMemory(to: UInt8.self).baseAddress, Int32(data.count))
            }
        } else {
            callback(nil, 0)
        }
    }
}
```

```csharp
// C# side with P/Invoke
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate void CaptureCallback(IntPtr data, int length);

[DllImport("ScreenCaptureHelper")]
static extern void sck_capture_window(
    uint windowID, int width, int height,
    CaptureCallback callback);
```

### Pattern 3: Timer-Based Capture with Fallback

**What:** Keep the existing 30-second timer architecture but swap the capture method.

**When to use:** This is the integration pattern -- it preserves the existing `IWindowCaptureService` contract.

```
Timer tick (30s)
    |
    v
[macOS >= 14?] ──YES──> SCK Helper: captureWindow(windowID) -> PNG bytes
    |                         |
    NO                        v
    |                    FrameCaptured event (byte[])
    v
CGDisplayCreateImage + SkiaSharp crop (existing code)
    |
    v
FrameCaptured event (byte[])
```

### Anti-Patterns to Avoid

- **SCStream for interval captures:** Do NOT set up a continuous SCStream to capture at 30-second intervals. SCStream is designed for continuous streaming (screensharing, recording). For 30-second snapshots, SCScreenshotManager is the correct tool. Using SCStream would waste memory and CPU.
- **Raw objc_msgSend for async APIs:** Do NOT try to call `SCShareableContent.getShareableContentWithCompletionHandler:` directly via `objc_msgSend`. Marshaling ObjC blocks (closures) from C# is error-prone and requires careful GC pinning. Use the native Swift helper instead.
- **Attempting to use .NET managed SCK bindings on .NET 8:** The bindings do not exist in the `net8.0-maccatalyst` reference assembly. Verified by direct assembly inspection.
- **Upgrading to .NET 9 just for SCK:** The Mac Catalyst SCK bindings in .NET 9 are still new (added Xcode 16.2 beta) and may be incomplete. A native helper is more reliable for .NET 8.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| GPU content capture | CGDisplayCreateImage workarounds (shader tricks, etc.) | ScreenCaptureKit via native helper | Apple designed SCK specifically for this; no workaround exists for CGDisplayCreateImage missing Metal content |
| CMSampleBuffer to image conversion | Custom pixel buffer parsing | CIContext.createCGImage or NSBitmapImageRep(cgImage:) in Swift helper | Multiple pixel formats, color space handling, Retina scaling -- too many edge cases |
| ObjC async callback marshaling | Hand-written block trampolines in C# | Native Swift helper that handles async internally | ObjC block ABI is complex; GC-unsafe pointers, capture semantics |
| Window enumeration | Replace CGWindowListCopyWindowInfo with SCShareableContent | Keep CGWindowListCopyWindowInfo for enumeration | It works fine, is already implemented, and doesn't need SCK. Optionally supplement with SCShareableContent for windowID validation |
| Permission checking | Custom TCC database queries | CGPreflightScreenCaptureAccess (existing) + SCShareableContent availability check | Apple's supported APIs; TCC database format is private |

**Key insight:** The native Swift helper approach keeps all the complex Apple-framework interaction (async/await, delegate patterns, memory management) in Swift where it's natural, and exposes a simple synchronous-result-via-callback interface to .NET. This is the same pattern used by the .NET MAUI Community Toolkit's Native Library Interop.

## Common Pitfalls

### Pitfall 1: ScreenCaptureKit crashes on Mac Catalyst

**What goes wrong:** SCShareableContent requests crash with "unrecognized selector" on Mac Catalyst apps, especially on macOS versions prior to 15.2.
**Why it happens:** ScreenCaptureKit was originally macOS-only (AppKit). Mac Catalyst support was only officially added in macCatalyst 18.2 (corresponds to macOS 15.2, Xcode 16.2). Earlier macOS versions don't have the UIKit-compatible SCK symbols.
**How to avoid:** Runtime version check before calling SCK. Fall back to CGDisplayCreateImage on older macOS. The native Swift helper should include `@available(macCatalyst 18.2, macOS 14.0, *)` guards.
**Warning signs:** EXC_BAD_ACCESS or "unrecognized selector sent to instance" crashes.

### Pitfall 2: GetShareableContentAsync hangs without run loop

**What goes wrong:** SCShareableContent async call never returns.
**Why it happens:** ScreenCaptureKit requires an active NSRunLoop/main event loop. In .NET console apps or background threads without a run loop, the completion handler never fires.
**How to avoid:** Always call SCK from the main thread or ensure an NSRunLoop is running. In MAUI MacCatalyst apps, the main thread has a run loop (UIApplication), so this should not be an issue IF calls are dispatched correctly. The native Swift helper should use `Task { @MainActor in ... }` or dispatch to main queue.
**Warning signs:** App freezes / deadlock when calling capture.

### Pitfall 3: Screen Recording permission with ad-hoc signing

**What goes wrong:** Permission dialog appears on every build, or CGPreflightScreenCaptureAccess returns false even when permission is granted.
**Why it happens:** Ad-hoc signed apps get a new code identity on each build. macOS TCC tracks permission by code identity.
**How to avoid:** This is already handled in the current code with comments. Both CGDisplayCreateImage and SCK use the same Screen Recording permission. The current `EnsureScreenRecordingPermission()` approach works. SCShareableContent will also trigger the permission dialog if not yet granted.
**Warning signs:** Blank captures, black images, or repeated permission dialogs.

### Pitfall 4: SCK pixel format mismatch with SkiaSharp

**What goes wrong:** Captured image appears with wrong colors (blue/red channels swapped) or is completely garbled.
**Why it happens:** SCK defaults to `kCVPixelFormatType_420YpCbCr8BiPlanarVideoRange` (YUV format). SkiaSharp expects BGRA or RGBA.
**How to avoid:** Set `SCStreamConfiguration.pixelFormat = kCVPixelFormatType_32BGRA` explicitly in the Swift helper. This is the most compatible format for downstream PNG encoding.
**Warning signs:** Color-shifted images, garbled pixels.

### Pitfall 5: Retina scaling / contentRect confusion

**What goes wrong:** Captured image is wrong size, or contains extra transparent areas.
**Why it happens:** SCK returns images in the display's native resolution (Retina 2x). The `contentRect` attachment on the CMSampleBuffer describes where the actual window content is within the captured surface.
**How to avoid:** Set `captureResolution = .best` and specify exact `width`/`height` in SCStreamConfiguration to match the window's point dimensions multiplied by scale factor. Or use `.automatic` and handle scaling after.
**Warning signs:** Images are 2x expected size, have black borders, or show desktop background in margins.

### Pitfall 6: CGDisplayCreateImage fallback still captures transparent Metal windows

**What goes wrong:** Fallback path still shows transparent windows for Metal/GPU games.
**Why it happens:** CGDisplayCreateImage fundamentally cannot capture GPU-composited content. This is the whole reason for the SCK migration.
**How to avoid:** Accept that the fallback path has this limitation. Document it clearly in the UI. On macOS < 14, this is an unsolvable limitation without SCK.
**Warning signs:** N/A -- this is a known, accepted limitation of the fallback path.

## Code Examples

### Example 1: Version check for SCK availability

```csharp
// Source: Apple platform versioning docs + existing project patterns
// Check if we can use ScreenCaptureKit (macOS 14+ required for SCScreenshotManager)
private static bool IsScreenCaptureKitAvailable()
{
    // In Mac Catalyst, iOS version numbers map to macOS versions:
    // maccatalyst 17.0 = macOS 14.0 (Sonoma) -- SCScreenshotManager introduced
    // maccatalyst 18.2 = macOS 15.2 (Sequoia) -- SCK officially available on Mac Catalyst
    if (OperatingSystem.IsMacCatalystVersionAtLeast(18, 2))
        return true;

    // On native macOS (if we ever switch from Catalyst):
    // if (OperatingSystem.IsMacOSVersionAtLeast(14, 0))
    //     return true;

    return false;
}
```

### Example 2: Updated WindowCaptureService with SCK integration

```csharp
// Source: Existing WindowCaptureService.cs + SCK helper pattern
private byte[] CaptureFrame(CaptureTarget target)
{
    if (IsScreenCaptureKitAvailable())
    {
        // Use SCK native helper
        var result = CaptureWithScreenCaptureKit(target);
        if (result.Length > 0) return result;
        // Fall through to legacy if SCK fails
    }

    // Legacy fallback: CGDisplayCreateImage + SkiaSharp crop
    return CaptureDisplayAndCrop(target);
}

private byte[] CaptureWithScreenCaptureKit(CaptureTarget target)
{
    var tcs = new TaskCompletionSource<byte[]>();

    // Call native Swift helper via ObjC interop
    // GaimerScreenCapture.captureWindow(windowID, width, height, completion)
    var classHandle = ObjCRuntime.Class.GetHandle("GaimerScreenCapture");
    if (classHandle == IntPtr.Zero) return [];

    // ... invoke via runtime messaging with callback ...
    // The native helper returns NSData containing PNG bytes

    // Block until complete (with timeout)
    if (!tcs.Task.Wait(TimeSpan.FromSeconds(10)))
        return [];

    return tcs.Task.Result;
}
```

### Example 3: Building and linking the xcframework

```bash
# Build the Swift framework for Mac Catalyst
# From the ScreenCaptureHelper Xcode project:
xcodebuild archive \
    -scheme ScreenCaptureHelper \
    -destination "generic/platform=macOS,variant=Mac Catalyst" \
    -archivePath build/maccatalyst \
    SKIP_INSTALL=NO BUILD_LIBRARY_FOR_DISTRIBUTION=YES

# Create xcframework
xcodebuild -create-xcframework \
    -framework build/maccatalyst.xcarchive/Products/Library/Frameworks/ScreenCaptureHelper.framework \
    -output build/ScreenCaptureHelper.xcframework
```

```xml
<!-- In GaimerDesktop.csproj -->
<ItemGroup Condition="$(TargetFramework.Contains('maccatalyst'))">
    <NativeReference Include="Platforms\MacCatalyst\ScreenCaptureHelper.xcframework">
        <Kind>Framework</Kind>
        <SmartLink>True</SmartLink>
    </NativeReference>
</ItemGroup>
```

### Example 4: CGImage to PNG bytes in Swift helper (what the helper does internally)

```swift
// Source: Apple Developer Docs - NSBitmapImageRep
// This happens inside the Swift helper -- .NET never sees CMSampleBuffer
let cgImage = try await SCScreenshotManager.captureImage(
    contentFilter: filter, configuration: config
)

// Convert CGImage -> PNG Data
let bitmapRep = NSBitmapImageRep(cgImage: cgImage)
guard let pngData = bitmapRep.representation(using: .png, properties: [:]) else {
    return nil
}
return pngData // This is what gets returned to .NET as NSData -> byte[]
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `CGWindowListCreateImage` | `SCScreenshotManager.captureImage` | macOS 14 (WWDC23) | Single-frame capture replacement; deprecated in macOS 15 |
| `CGDisplayCreateImage` + crop | `SCScreenshotManager.captureImage` with `SCContentFilter(desktopIndependentWindow:)` | macOS 12.3 (SCK) / macOS 14 (screenshot API) | GPU content now captured correctly |
| N/A (SCK macOS only) | SCK available on Mac Catalyst | macCatalyst 18.2 / Xcode 16.2 | First time SCK can be used from UIKit/Catalyst apps |
| Raw `objc_msgSend` interop | Native Library Interop (xcframework + binding) | .NET MAUI community toolkit 2024 | Cleaner, maintainable pattern for Swift API consumption |

**Deprecated/outdated:**
- `CGWindowListCreateImage`: Deprecated/removed in macOS 15. Do NOT use. The project's NativeMethods.cs still has this P/Invoke but the capture service already doesn't call it.
- `CGDisplayCreateImage`: Not deprecated but cannot capture GPU content. Keep as fallback only.
- Direct `ObjCRuntime.Messaging.objc_msgSend` for complex APIs: Replaced by Native Library Interop pattern. Still works but brittle for async/callback patterns.

## Open Questions

1. **Exact xcframework build + link workflow for this project**
   - What we know: The Native Library Interop pattern uses Xcode to build a Swift framework, creates an xcframework, and links it via `NativeReference` in the csproj.
   - What's unclear: Whether a full Xcode project is needed or if `swiftc` command-line compilation suffices for a single-file framework. Also unclear: does the framework need to be pre-built and committed, or built as part of the MAUI build pipeline?
   - Recommendation: Start with a minimal Xcode project (1 Swift file). Pre-build the xcframework and commit it to the repo. This avoids requiring Xcode as a build dependency for the MAUI project.

2. **ObjC block marshaling from C# for the completion handler**
   - What we know: The Swift helper uses completion handlers (`@escaping (NSData?) -> Void`). In .NET, ObjC blocks can be marshaled, but it's non-trivial.
   - What's unclear: Exact syntax for creating an ObjC block delegate in .NET 8 MacCatalyst and passing it via `objc_msgSend`.
   - Recommendation: Two options: (a) Use `@_cdecl` with C function pointers instead of ObjC blocks (simpler P/Invoke), or (b) have the Swift helper write the result to a temp file and signal completion via a simpler mechanism (e.g., return a file path synchronously after blocking internally). Option (a) is preferred.

3. **Mac Catalyst 18.2 version check accuracy**
   - What we know: `OperatingSystem.IsMacCatalystVersionAtLeast(18, 2)` should check the version. There are known issues with Mac Catalyst version checks not mapping correctly to macOS versions.
   - What's unclear: Whether `IsMacCatalystVersionAtLeast` works correctly on all macOS versions, or if we need `NSProcessInfo.processInfo.operatingSystemVersion` via interop.
   - Recommendation: Test the version check on the development machine (macOS 15.7.4 should pass). Add a secondary fallback check via native helper.

4. **Whether SCK works from Mac Catalyst on macOS 15.7 (current dev machine)**
   - What we know: Official SCK Mac Catalyst support is macCatalyst 18.2. The dev machine runs macOS 15.7.4.
   - What's unclear: macCatalyst 18.2 maps to approximately macOS 15.2. The dev machine at 15.7.4 should be fine, but this needs actual testing.
   - Recommendation: Build a minimal test to verify SCK works from a Mac Catalyst app on this machine before committing to the full implementation.

5. **Performance of SCScreenshotManager vs CGDisplayCreateImage**
   - What we know: CGDisplayCreateImage captures the full display and requires SkiaSharp cropping. SCScreenshotManager captures a specific window directly.
   - What's unclear: Latency of SCScreenshotManager calls (involves async round-trip through WindowServer).
   - Recommendation: SCScreenshotManager should be faster in practice since it avoids full-display capture + crop. But at 30-second intervals, even a 500ms capture is acceptable.

## Sources

### Primary (HIGH confidence)
- **Direct assembly inspection**: `/usr/local/share/dotnet/packs/Microsoft.MacCatalyst.Ref.net8.0_18.0/18.0.8319/ref/net8.0/Microsoft.MacCatalyst.dll` -- Confirmed zero ScreenCaptureKit types, confirmed presence of CMSampleBuffer, CVPixelBuffer, CGImage, IOSurface
- **Apple Developer Docs**: [SCScreenshotManager](https://developer.apple.com/documentation/screencapturekit/scscreenshotmanager) -- macOS 14.0+, captures single frames as CGImage
- **Apple Developer Docs**: [SCContentFilter(desktopIndependentWindow:)](https://developer.apple.com/documentation/screencapturekit/sccontentfilter/init(desktopindependentwindow:)) -- Single window capture filter
- **Apple Developer Docs**: [stream(_:didOutputSampleBuffer:of:)](https://developer.apple.com/documentation/screencapturekit/scstreamoutput/stream(_:didoutputsamplebuffer:of:)) -- Stream output delegate (for reference, not primary approach)
- **Apple WWDC22**: [Meet ScreenCaptureKit](https://developer.apple.com/videos/play/wwdc2022/10156/) -- Framework overview, SCStream architecture
- **Apple WWDC23**: [What's new in ScreenCaptureKit](https://developer.apple.com/videos/play/wwdc2023/10136/) -- SCScreenshotManager introduction

### Secondary (MEDIUM confidence)
- **dotnet/macios wiki**: [ScreenCaptureKit macOS xcode16.2 b2](https://github.com/xamarin/xamarin-macios/wiki/ScreenCaptureKit-macOS-xcode16.2-b2) -- Mac Catalyst 18.2 availability annotations added
- **dotnet/macios wiki**: [.NET 8 release notes](https://github.com/dotnet/macios/wiki/.NET-8-release-notes) -- Xcode 15 SCK support added (macOS only, not Mac Catalyst)
- **Microsoft Docs**: [Native Library Interop](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/) -- xcframework + Swift wrapper pattern for MAUI
- **Microsoft Docs**: [Getting Started with Native Library Interop](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/maui/native-library-interop/get-started) -- Step-by-step Xcode project setup
- **dotnet/macios issue #17350**: [ScreenCaptureKit hangs on awaiting GetShareableContentAsync](https://github.com/dotnet/macios/issues/17350) -- Run loop requirement confirmed

### Tertiary (LOW confidence)
- **Apple Developer Forums**: [ScreenCaptureKit crashes on Mac Catalyst apps](https://developer.apple.com/forums/thread/713630) -- Known crash issue (could not fetch full thread content; page requires JavaScript)
- **Nonstrict blog**: [A look at ScreenCaptureKit on macOS Sonoma](https://nonstrict.eu/blog/2023/a-look-at-screencapturekit-on-macos-sonoma/) -- SCScreenshotManager overview
- **JUCE issue #1414**: [CGWindowListCreateImage is obsolete in macOS 15](https://github.com/juce-framework/JUCE/issues/1414) -- Confirms deprecation

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- Verified via direct assembly inspection and Apple documentation
- Architecture (native helper pattern): MEDIUM -- Pattern is well-documented by Microsoft, but the specific SCK-in-Mac-Catalyst combination has limited community examples
- Architecture (SCScreenshotManager API): HIGH -- Apple's official API, well-documented
- Pitfalls: MEDIUM -- Mac Catalyst crash issue is documented but full details couldn't be fetched; version mapping needs validation
- Code examples: MEDIUM -- Swift side is straightforward from Apple docs; C# interop calling convention needs implementation-time validation

**Research date:** 2026-02-25
**Valid until:** 2026-04-25 (stable -- Apple framework APIs don't change between major releases)
