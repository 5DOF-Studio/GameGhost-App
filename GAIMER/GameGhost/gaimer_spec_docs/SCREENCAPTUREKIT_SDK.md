# GaimerScreenCapture SDK Reference

Native Swift xcframework bridging ScreenCaptureKit to .NET MAUI via P/Invoke.

---

## 1. Overview

.NET 8 MacCatalyst has no managed bindings for ScreenCaptureKit (SCK). The legacy `CGWindowListCreateImage` API cannot capture GPU/Metal-rendered content (games render black). This xcframework solves both problems: it wraps SCK's `SCScreenshotManager` in a thin Swift library that exports C-callable functions consumable by .NET P/Invoke.

**Key files:**

| File | Role |
|------|------|
| `NativeHelpers/GaimerScreenCapture/Sources/GaimerScreenCapture/GaimerScreenCapture.swift` | Swift implementation |
| `NativeHelpers/GaimerScreenCapture/Package.swift` | Swift Package Manager manifest |
| `NativeHelpers/GaimerScreenCapture/build-xcframework.sh` | Build script |
| `Platforms/MacCatalyst/NativeMethods.cs` | P/Invoke declarations |
| `Platforms/MacCatalyst/WindowCaptureService.cs` | C# integration service |

All paths relative to `src/GaimerDesktop/GaimerDesktop/`.

---

## 2. Architecture

```
WindowCaptureService.cs          (C# - timer thread)
    |
    | P/Invoke [DllImport("GaimerScreenCapture")]
    v
GaimerScreenCapture.swift        (@_cdecl exported C functions)
    |
    | Task.detached(priority: .userInitiated)
    v
SCScreenshotManager.captureImage (ScreenCaptureKit - cooperative pool)
    |
    | CGImage -> ImageIO -> PNG Data
    v
callback(pngBytesPtr, length)    (C function pointer back to C#)
    |
    | Marshal.Copy -> byte[]
    v
TaskCompletionSource.SetResult   (C# - completes async bridge)
```

The boundary is two `@_cdecl` functions with C calling convention. No Objective-C blocks, no NSObject marshaling, no Swift async/await crossing the ABI -- just scalar types and a C function pointer.

---

## 3. Exported Functions

### `sck_is_available() -> Bool`

```swift
@_cdecl("sck_is_available")
public func sckIsAvailable() -> Bool
```

Returns `true` if ScreenCaptureKit is available on the current system. Availability check:
- macCatalyst 18.2+ (maps to macOS 14.0+)
- macOS 14.0+

**C# declaration:**
```csharp
[DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.U1)]
internal static extern bool sck_is_available();
```

Called once at class load, result cached in `WindowCaptureService._sckAvailable`. SCK availability does not change at runtime.

### `sck_capture_window(windowID, width, height, callback)`

```swift
@_cdecl("sck_capture_window")
public func sckCaptureWindow(
    windowID: UInt32,      // CGWindowID of target window
    width: Int32,          // Desired capture width (pixels)
    height: Int32,         // Desired capture height (pixels)
    callback: @convention(c) (UnsafePointer<UInt8>?, Int32) -> Void
)
```

Captures a single window frame and delivers PNG bytes via the callback.

**Parameters:**
- `windowID` -- CGWindowID obtained from `CGWindowListCopyWindowInfo`
- `width`, `height` -- Capture dimensions. C# passes `boundsWidth * 2` / `boundsHeight * 2` for Retina resolution
- `callback` -- C function pointer. Called with `(pngBytesPointer, byteCount)` on success, `(nil, 0)` on failure

**C# declaration:**
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void SckCaptureCallback(IntPtr data, int length);

[DllImport("GaimerScreenCapture", CallingConvention = CallingConvention.Cdecl)]
internal static extern void sck_capture_window(
    uint windowID, int width, int height,
    SckCaptureCallback callback);
```

**Callback lifetime:** The delegate must be pinned via `GCHandle.Alloc` for the duration of the native call to prevent GC collection. Released in a `finally` block after `TaskCompletionSource` completes or times out.

---

## 4. Threading Model

```
Timer Thread                    Cooperative Thread Pool
-----------                    -----------------------
OnCaptureTimerTick()
  |
  +-- NativeMethods.sck_capture_window(id, w, h, callback)
  |     |
  |     +-- [Swift] Task.detached(priority: .userInitiated)
  |     |       |
  +-- tcs.Task.Wait(10s) [blocks]    SCScreenshotManager.captureImage
  |     |                             |
  |     |                            cgImageToPNGData()
  |     |                             |
  |     |                            callback(ptr, len)
  |     |                             |
  +-- [callback] Marshal.Copy -> tcs.SetResult
  |
  +-- tcs.Task.Result -> byte[]
  |
  +-- FrameCaptured?.Invoke(bytes)
  |
  +-- Re-arm one-shot timer
```

Key points:
- **Task.detached, NOT @MainActor.** The Swift side uses `Task.detached(priority: .userInitiated)` to dispatch SCK work onto the cooperative thread pool. Using `@MainActor` causes deadlocks: the C# timer thread blocks on `Task.Wait()` while `@MainActor` needs the main thread to run its continuation -- neither can proceed. This was discovered by researching the Jiga-MacOS predecessor app, which used the same `Task.detached` pattern. See Decision D-012.
- **Timer uses one-shot pattern.** `Timer(callback, null, 0, Timeout.Infinite)` fires once; re-armed in the `finally` block with `Change(30000, Timeout.Infinite)`. Prevents tick overlap if capture takes longer than the interval.
- **TaskCompletionSource bridges async callback to sync wait.** Created with `RunContinuationsAsynchronously` to prevent continuation inlining on the main thread.
- **10-second timeout** prevents permanent hangs. Late callbacks after timeout are discarded via `Volatile.Read(ref timedOut)`.
- **NSLog diagnostics** in the Swift helper trace each step of the capture flow for debugging.

---

## 5. Image Pipeline

```
SCScreenshotManager.captureImage(contentFilter, configuration)
    |
    v
CGImage (BGRA pixel format, Retina resolution)
    |
    v
cgImageToPNGData()
    |-- CGImageDestinationCreateWithData(NSMutableData, UTType.png)
    |-- CGImageDestinationAddImage(dest, cgImage)
    |-- CGImageDestinationFinalize(dest)
    |
    v
Data (PNG bytes in Swift memory)
    |
    v
data.withUnsafeBytes { callback(baseAddress, count) }
    |
    v
Marshal.Copy(dataPtr, buffer, 0, length)  [C# side]
    |
    v
byte[] (managed .NET array, safe after callback returns)
```

**Why ImageIO, not AppKit?** `NSBitmapImageRep` requires AppKit, which is unavailable on Mac Catalyst. ImageIO's `CGImageDestination` works on all Apple platforms.

**SCK configuration:**
- `pixelFormat = kCVPixelFormatType_32BGRA` -- SkiaSharp expects BGRA, not the default YUV
- `captureResolution = .best` -- Retina quality (produces full Retina resolution, e.g. 2562x2326 for Chess.app)
- `showsCursor = true` -- Cursor visible in capture (useful for debugging; can be set to false for clean game capture in production)

---

## 6. Fallback Strategy

```
if (_sckAvailable)
    frameData = CaptureWithScreenCaptureKit(target)
    if (frameData.Length == 0)
        frameData = CaptureDisplayAndCrop(target)      // per-frame fallback
else
    frameData = CaptureDisplayAndCrop(target)           // permanent fallback
```

**SCK path (macOS 14+ / MacCatalyst 18.2+):**
`SCScreenshotManager.captureImage` -> CGImage -> PNG via ImageIO

**Legacy fallback:**
`CGDisplayCreateImage(displayId)` -> UIImage -> PNG -> SKBitmap -> crop to window bounds -> re-encode PNG

The fallback captures the entire display and crops to the target window bounds using SkiaSharp. It cannot capture GPU-rendered content (games appear black) but works for non-game windows and older macOS versions.

SCK availability is checked once at startup. If SCK capture fails on a specific frame (window disappeared, permission revoked), the fallback is used for that frame only -- next tick retries SCK first.

---

## 7. Building the xcframework

**Prerequisites:** Xcode with Mac Catalyst support, Swift 5.9+

```bash
cd src/GaimerDesktop/NativeHelpers/GaimerScreenCapture
chmod +x build-xcframework.sh
./build-xcframework.sh
```

**What the script does:**
1. Cleans `.build-xcframework/` directory
2. Runs `xcodebuild build` for Mac Catalyst Release with `BUILD_LIBRARY_FOR_DISTRIBUTION=YES`
3. Locates the built framework (or creates framework structure from bare dylib)
4. Wraps in xcframework via `xcodebuild -create-xcframework`
5. Copies result to `Platforms/MacCatalyst/GaimerScreenCapture.xcframework`

**Swift Package manifest** (`Package.swift`):
- Swift tools version 5.9
- Platforms: macCatalyst 16+, macOS 14+
- Product: dynamic library
- Linked frameworks: ScreenCaptureKit, CoreGraphics, CoreImage, UIKit (Catalyst only)

The xcframework is checked into the repo. Rebuild only when modifying `GaimerScreenCapture.swift`.

---

## 8. Linking in the MAUI Project

In `GaimerDesktop.csproj`:

```xml
<!-- Native Swift helper for ScreenCaptureKit (Mac Catalyst only) -->
<ItemGroup Condition="$(TargetFramework.Contains('maccatalyst'))">
    <NativeReference Include="Platforms\MacCatalyst\GaimerScreenCapture.xcframework">
        <Kind>Framework</Kind>
        <SmartLink>True</SmartLink>
    </NativeReference>
</ItemGroup>
```

- Conditional on `maccatalyst` TFM -- Windows/Android/iOS builds ignore it
- `Kind=Framework` tells the MAUI build system to embed the framework in the app bundle
- `SmartLink=True` enables dead-code stripping
- The `DllImport("GaimerScreenCapture")` name must match the framework binary name (no `lib` prefix, no `.dylib` extension)

---

## 9. Permissions & TCC Resolution

### Screen Recording (TCC)

ScreenCaptureKit and `CGDisplayCreateImage` both require Screen Recording permission, managed by macOS TCC (Transparency, Consent, and Control).

**Permission check:**
```csharp
NativeMethods.CGPreflightScreenCaptureAccess();  // check without prompting
```

Called in `EnsureScreenRecordingPermission()` before window enumeration. The code logs the preflight result but does not gate on it -- capture proceeds regardless.

### Apple Development Certificate (Required)

TCC requires a **stable code identity** to persist Screen Recording permission grants. Ad-hoc signing (`codesign --sign -`) generates a new identity per build, causing macOS to treat each build as an unrecognized app.

**Required:** Sign with an Apple Development certificate:
```bash
codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" \
    --entitlements /tmp/GaimerDesktop.entitlements \
    /Applications/GaimerDesktop.app
```

**Entitlements file** (`/tmp/GaimerDesktop.entitlements`):
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN"
    "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.app-sandbox</key>
    <false/>
</dict>
</plist>
```

Without the entitlements embedded during signing, the app may be sandboxed and SCK access denied.

### Deploy Location

Deploy to `/Applications/GaimerDesktop.app`, not `/tmp/`. macOS TCC uses the app's filesystem location as part of identity resolution. Apps in `/tmp/` show as "(null)" in System Settings > Privacy & Security > Screen Recording, making permission management impossible. `/Applications/` is a standard install location that TCC recognizes properly.

### CGRequestScreenCaptureAccess on macOS Sequoia

**Do NOT call `CGRequestScreenCaptureAccess()` on macOS Sequoia (15.x).** On Sequoia, this API opens System Settings on every app launch instead of showing a one-time permission dialog. This is a known Apple behavior change. The app should rely on:
1. The user pre-granting permission in System Settings, or
2. SCK returning error -3801, with the app guiding the user to System Settings manually.

### Clearing Stale TCC Entries

When switching signing identities (e.g., from ad-hoc to Apple Development cert), stale TCC entries may conflict. Reset with:
```bash
tccutil reset ScreenCapture com.5dof.gaimer
```

This clears all Screen Recording permission entries for the bundle ID, forcing a clean permission prompt on next launch. Use this when:
- Switching from ad-hoc to Apple Development signing
- Permission is denied despite being granted in System Settings
- System Settings shows "(null)" or duplicate entries for the app

### TCC Error Reference

| Error | Meaning | Resolution |
|-------|---------|------------|
| -3801 | "The user declined TCCs" | Grant Screen Recording in System Settings; clear stale entries with `tccutil reset` |
| Preflight returns false | Permission not granted or unstable identity | Switch to Apple Development cert; check System Settings |
| "(null)" in System Settings | App launched from transient location (/tmp) | Deploy to /Applications/ |

### Ad-hoc Signing (Legacy -- Not Recommended)

Ad-hoc signing still works for building and basic app functionality but causes persistent TCC issues:
- Screen Recording permission must be re-granted after each build
- `CGPreflightScreenCaptureAccess` returns `false` even when previously granted
- SCK returns error -3801 intermittently

Use Apple Development certificate signing instead.

---

## 10. Known Limitations

| Limitation | Details |
|-----------|---------|
| **macOS version** | SCK requires macOS 14.0+ / MacCatalyst 18.2+. Older versions fall back to `CGDisplayCreateImage` (no GPU capture). |
| **Ad-hoc signing** | Each `dotnet build` + `codesign` cycle creates a new code identity. Screen Recording permission dialog reappears. Workaround: use a stable Developer ID or development certificate. |
| **Window enumeration** | Uses `CGWindowListCopyWindowInfo` (not SCK's `SCShareableContent`) for metadata/enumeration. SCK is only used for frame capture. |
| **Single-frame only** | Uses `SCScreenshotManager` (single frame), not `SCStream` (continuous). Appropriate for the 30-second capture interval. Not suitable for real-time streaming. |
| **Thumbnail generation** | Thumbnails use the legacy `CGDisplayCreateImage` path, not SCK. GPU-rendered thumbnails may appear black. Acceptable since thumbnails are for window selection UI. |
| **No audio capture** | The xcframework captures video frames only. Audio capture is handled separately by the voice pipeline. |
| **Memory** | PNG bytes are copied across the native/managed boundary via `Marshal.Copy`. For a 1920x1080 Retina capture, the PNG is typically 2-5 MB. The copy is necessary because Swift memory is freed after the callback returns. |

---

## 11. DllImport Resolution

### Problem

.NET's default `DllImport` resolution cannot find xcframeworks embedded in a Mac Catalyst app bundle. The framework binary exists at:
```
GaimerDesktop.app/Contents/Frameworks/GaimerScreenCapture.framework/GaimerScreenCapture
```

But `[DllImport("GaimerScreenCapture")]` throws `DllNotFoundException` because the .NET runtime's search paths do not include the Frameworks directory within the Mac Catalyst app bundle layout.

### Solution

A `NativeLibrary.SetDllImportResolver` is registered in the `NativeMethods` static constructor:

```csharp
static NativeMethods()
{
    NativeLibrary.SetDllImportResolver(
        typeof(NativeMethods).Assembly,
        (libraryName, assembly, searchPath) =>
        {
            if (libraryName == "GaimerScreenCapture")
            {
                // Probe the app bundle's Frameworks directory
                var bundlePath = NSBundle.MainBundle.BundlePath;
                var frameworkPath = Path.Combine(
                    bundlePath, "Contents", "Frameworks",
                    "GaimerScreenCapture.framework",
                    "GaimerScreenCapture");

                if (NativeLibrary.TryLoad(frameworkPath, out var handle))
                    return handle;
            }

            // Fall through to default resolution for other libraries
            return IntPtr.Zero;
        });
}
```

### Key Details

- **Static constructor guarantees single registration.** The resolver is set once before any P/Invoke call.
- **Only intercepts "GaimerScreenCapture".** All other library names fall through to .NET's default resolution (important for system libraries like CoreGraphics).
- **Uses `NativeLibrary.TryLoad`** for safe probing -- returns false instead of throwing if the path doesn't exist.
- **`NSBundle.MainBundle.BundlePath`** provides the correct app bundle root regardless of where the app is launched from.
- **Mac Catalyst bundle layout** places frameworks at `AppName.app/Contents/Frameworks/` (unlike iOS which uses `AppName.app/Frameworks/`).

### Diagnostics

If the resolver fails, `DllNotFoundException` will still be thrown. Check:
1. The xcframework is listed in `.csproj` as a `NativeReference` with `Kind=Framework`
2. The framework binary exists in the built `.app` bundle under `Contents/Frameworks/`
3. The framework name matches exactly (case-sensitive, no `lib` prefix, no `.dylib` extension)
