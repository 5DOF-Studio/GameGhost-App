# Seven Layers Deep: Getting ScreenCaptureKit Working in .NET MAUI Mac Catalyst

**Project:** Gaimer Desktop (.NET MAUI)
**Author:** Tony Nlemadim
**Date:** February 2026
**Category:** Build in Public — Research Paper / Post-Mortem
**Status:** Resolved. Screen capture operational.

---

## TL;DR

Getting Apple's ScreenCaptureKit (SCK) to capture GPU-rendered game content from a .NET MAUI Mac Catalyst app required solving seven distinct layers of platform interop problems, each one hidden behind the last:

| Layer | Problem | Fix |
|-------|---------|-----|
| 1 | No managed bindings for SCK in .NET | Built a Swift xcframework with `@_cdecl` exports and P/Invoke |
| 2 | `@MainActor` callback deadlocks C# thread | Switched to `Task.detached(priority: .userInitiated)` |
| 3 | `DllNotFoundException` despite framework in bundle | `NativeLibrary.SetDllImportResolver` to manually resolve framework path |
| 4 | TCC permission denied (-3801) | Called `CGRequestScreenCaptureAccess()` to trigger consent prompt |
| 5 | Sequoia removed the permission dialog | Removed `CGRequestScreenCaptureAccess()` call; manual System Settings grant |
| 6 | Ad-hoc signing = new identity every build = TCC amnesia | Signed with stable Apple Development certificate |
| 7 | Missing entitlements + wrong deploy location + stale TCC | Embedded entitlements via `--entitlements`, deployed to `/Applications/`, ran `tccutil reset` |

Total debug time: multiple sessions across days. Total code change: ~200 lines of Swift, ~150 lines of C#, one build script, one entitlements file.

The chess board is now visible. Full Retina resolution. GPU-rendered Metal content captured successfully.

---

## Table of Contents

1. [The Problem](#the-problem)
2. [Layer 1: Building the Swift Bridge](#layer-1-building-the-swift-bridge)
3. [Layer 2: The @MainActor Deadlock Risk](#layer-2-the-mainactor-deadlock-risk)
4. [Layer 3: DllNotFoundException — The Framework is Right There](#layer-3-dllnotfoundexception--the-framework-is-right-there)
5. [Layer 4: TCC Permission Denied (-3801)](#layer-4-tcc-permission-denied--3801)
6. [Layer 5: Sequoia Changed the Rules](#layer-5-sequoia-changed-the-rules)
7. [Layer 6: Ad-hoc Signing Identity Crisis](#layer-6-ad-hoc-signing-identity-crisis)
8. [Layer 7: Missing Entitlements + Wrong Location](#layer-7-missing-entitlements--wrong-location)
9. [The Breakthrough](#the-breakthrough)
10. [Lessons Learned](#lessons-learned)
11. [The Final Build Command](#the-final-build-command)

---

## The Problem

GAIMER Desktop is a .NET MAUI app that watches gameplay via screen capture and sends frames to an AI brain for real-time analysis. The first target game is Apple Chess.app — a built-in macOS application that renders its board and pieces using SceneKit backed by Metal (GPU).

The legacy macOS capture API, `CGDisplayCreateImage` / `CGWindowListCreateImage`, works by compositing from the window server's buffer. But GPU-rendered content — Metal, SceneKit, OpenGL — bypasses that buffer. When we captured the Chess window with the legacy API, the preview showed the desktop wallpaper. The chess board was invisible.

Apple introduced ScreenCaptureKit (SCK) in macOS 12.3 specifically to solve this. SCK captures directly from the GPU compositor, seeing everything the user sees. It is the only path forward for capturing modern game content.

But here is the catch: **.NET MAUI has zero managed bindings for ScreenCaptureKit.** No NuGet package. No community wrapper. No official Microsoft support. The API exists only in Swift and Objective-C.

We had to bridge the gap ourselves. What followed was a seven-layer debugging odyssey through platform interop, native code signing, macOS security policy, and operating system version-specific behavior changes — each layer invisible until the previous one was solved.

---

## Layer 1: Building the Swift Bridge

### The approach

Since .NET cannot call Swift directly, we needed an interop boundary. The strategy: build a native Swift xcframework (`GaimerScreenCapture`) that exports C-callable functions using Swift's `@_cdecl` attribute. C functions can be consumed by .NET's P/Invoke mechanism — the same `[DllImport]` pattern used for calling into C libraries on every platform.

### The API surface

Two functions. That is all the bridge needs:

```swift
@_cdecl("sck_is_available")
public func sckIsAvailable() -> Bool

@_cdecl("sck_capture_window")
public func sckCaptureWindow(
    _ windowID: UInt32,
    _ width: Int32,
    _ height: Int32,
    _ callback: @escaping @convention(c) (UnsafePointer<UInt8>?, Int32) -> Void
)
```

The first checks whether SCK is available on the current system. The second captures a window by ID and calls back into C# with a pointer to PNG bytes and their length.

### The image conversion problem

SCK's `SCScreenshotManager.captureImage` returns a `CGImage`. We need PNG bytes to hand back across the P/Invoke boundary. The obvious approach on macOS is `NSBitmapImageRep` — but Mac Catalyst apps run in a UIKit sandbox. AppKit classes like `NSBitmapImageRep` are unavailable.

The fix: use ImageIO directly. `CGImageDestination` with `kUTTypePNG` works everywhere — macOS, Mac Catalyst, iOS. Lower-level, but universal:

```swift
guard let mutableData = CFDataCreateMutable(nil, 0),
      let dest = CGImageDestinationCreateWithData(mutableData, kUTTypePNG, 1, nil)
else { return }
CGImageDestinationAddImage(dest, cgImage, nil)
CGImageDestinationFinalize(dest)
let data = mutableData as Data
```

### The build script

A shell script compiles the Swift package for `x86_64-apple-ios-macabi` and `arm64-apple-ios-macabi` (the Mac Catalyst platform triplets), then uses `xcodebuild -create-xcframework` to produce a universal framework. The xcframework gets copied into the MAUI project's output bundle at build time.

**Layer 1 status:** Swift bridge compiled. Functions exported. Framework built. Time to call it from C#.

---

## Layer 2: The @MainActor Deadlock Risk

### The discovery

Before writing the C# side, we studied a predecessor project — Jiga-MacOS, an iOS app that had already integrated ScreenCaptureKit. Its capture code contained a critical pattern:

```swift
// Jiga's approach — background dispatch
DispatchQueue.global(qos: .userInitiated).async {
    // ... capture callback runs here
}
```

Jiga deliberately ran the capture callback on a background queue, NOT on `@MainActor`. Why?

### The deadlock scenario

Consider the C# side of the bridge:

```csharp
var tcs = new TaskCompletionSource<byte[]>();

NativeMethods.sck_capture_window(windowId, width, height, (ptr, len) => {
    var bytes = new byte[len];
    Marshal.Copy(ptr, bytes, 0, len);
    tcs.SetResult(bytes);
});

var result = tcs.Task.Result; // BLOCKS the current thread
```

If C# calls `tcs.Task.Result` on the main thread, and the Swift callback dispatches to `@MainActor` (which IS the main thread), we have a classic deadlock: C# is blocking the main thread waiting for a result that can only be delivered on the main thread.

### The fix

Our initial Swift implementation used `Task { @MainActor in ... }` for the capture. We changed it:

```swift
// BEFORE — deadlock risk
Task { @MainActor in
    let image = try await SCScreenshotManager.captureImage(...)
    callback(pngPointer, pngLength)
}

// AFTER — safe background dispatch
Task.detached(priority: .userInitiated) {
    let image = try await SCScreenshotManager.captureImage(...)
    callback(pngPointer, pngLength)
}
```

`Task.detached` runs on a cooperative thread pool with no actor affinity. The callback fires on a background thread, and C# can safely block or await without deadlocking the main thread.

**Layer 2 status:** Deadlock risk eliminated before it could manifest. Proactive debugging based on prior art.

---

## Layer 3: DllNotFoundException — The Framework is Right There

### The symptom

With the Swift framework built and copied into the app bundle, we ran the app:

```
[NativeMethods] SCK available check: DllNotFoundException: GaimerScreenCapture
```

SCK reported "not available." Not because ScreenCaptureKit was missing from the system, but because .NET could not find our native framework to even ask the question.

### The investigation

The framework was in the app bundle. We verified it:

```
GaimerDesktop.app/
  Contents/
    Frameworks/
      GaimerScreenCapture.framework/
        GaimerScreenCapture          <-- the dylib is RIGHT HERE
```

We checked the dylib's install name:

```bash
$ otool -D GaimerScreenCapture
@rpath/GaimerScreenCapture.framework/Versions/A/GaimerScreenCapture
```

The dylib uses `@rpath` — a dynamic run-path search variable that the Mach-O loader resolves at launch time. But .NET's `DllImport` does not go through the Mach-O loader for framework resolution. It calls `dlopen()` with the bare library name. `dlopen("GaimerScreenCapture")` does not search the app's `Frameworks/` directory. It searches `/usr/lib`, `@rpath` entries in the binary's LC_RPATH commands (which .NET does not set for third-party frameworks), and `DYLD_LIBRARY_PATH` (which is stripped by SIP).

### The fix

.NET provides a hook for exactly this scenario: `NativeLibrary.SetDllImportResolver`. In the static constructor of our P/Invoke class, we register a custom resolver:

```csharp
static NativeMethods()
{
    NativeLibrary.SetDllImportResolver(
        typeof(NativeMethods).Assembly,
        (libraryName, assembly, searchPath) =>
        {
            if (libraryName != "GaimerScreenCapture")
                return IntPtr.Zero;

            var bundlePath = NSBundle.MainBundle.BundlePath;

            // Try flat framework layout
            var path = Path.Combine(bundlePath, "Contents", "Frameworks",
                "GaimerScreenCapture.framework", "GaimerScreenCapture");
            if (File.Exists(path))
                return NativeLibrary.Load(path);

            // Try versioned framework layout
            path = Path.Combine(bundlePath, "Contents", "Frameworks",
                "GaimerScreenCapture.framework", "Versions", "A",
                "GaimerScreenCapture");
            if (File.Exists(path))
                return NativeLibrary.Load(path);

            return IntPtr.Zero;
        });
}
```

Result:

```
[NativeMethods] Successfully loaded GaimerScreenCapture framework
[NativeMethods] SCK is available: True
```

**Layer 3 status:** Framework loaded. SCK reports available. Time to capture something.

---

## Layer 4: TCC Permission Denied (-3801)

### The symptom

SCK loaded successfully. It found the Chess window:

```
Found window 1684: app=Chess, title=Game 1 | Tony Nlemadim - Computer (White to Move)
```

But the capture call failed:

```
SCK capture error: The user declined TCCs for application, window, display capture
Error code: -3801
```

### The cause

macOS TCC (Transparency, Consent, and Control) is the security subsystem that governs access to sensitive resources: camera, microphone, screen recording, files, contacts. ScreenCaptureKit requires Screen Recording permission. Without it, every capture call returns error -3801.

Our code was checking for permission with `CGPreflightScreenCaptureAccess()`, which returns a boolean indicating whether the app currently has permission. But we never called `CGRequestScreenCaptureAccess()` — the function that triggers the system permission dialog.

### The fix

Added the request call:

```swift
if !CGPreflightScreenCaptureAccess() {
    CGRequestScreenCaptureAccess()
}
```

Simple enough. Or so we thought.

**Layer 4 status:** Permission request added. But the dialog never appeared.

---

## Layer 5: Sequoia Changed the Rules

### The surprise

On macOS 14 Sonoma and earlier, `CGRequestScreenCaptureAccess()` displayed a system dialog:

> "GaimerDesktop would like to record this computer's screen."
> [Don't Allow] [OK]

On macOS 15 Sequoia, Apple changed the behavior. `CGRequestScreenCaptureAccess()` no longer shows a dialog. Instead, it:

1. Silently adds the app to the Screen Recording list in System Settings
2. Opens System Settings > Privacy & Security > Screen Recording
3. Returns `false`

Every app launch triggered System Settings to open. The user had to manually find the app in the list and toggle the switch. But there was no in-app indication of what was happening or why.

### The fix

Removed the `CGRequestScreenCaptureAccess()` call entirely. Instead, we check with `CGPreflightScreenCaptureAccess()` and, if permission is not granted, display an in-app message directing the user to System Settings > Privacy & Security > Screen Recording.

This is the pattern Apple now expects. The OS manages the permission UI, not the app.

**Layer 5 status:** Permission flow adapted for Sequoia. User grants access manually. But TCC still denied.

---

## Layer 6: Ad-hoc Signing Identity Crisis

### The symptom

The user navigated to System Settings, found GaimerDesktop in the Screen Recording list, toggled it on, restarted the app. TCC still denied. Error -3801 persisted.

### The root cause

Our build process used ad-hoc code signing:

```bash
codesign --force --deep --sign - /tmp/GaimerDesktop.app
```

The `-` sign identity means "ad-hoc" — the binary is signed but not with any certificate. Each build produces a **new, unique code signing identity**. macOS TCC tracks permissions by code signing identity. When the user granted Screen Recording permission to build N, build N+1 had a different identity. TCC did not recognize it.

The permission was granted to a ghost — a signing identity that no longer existed.

### The fix

We needed a stable signing identity that persists across builds. The development Mac had an Apple Development certificate installed:

```
Apple Development: Ike Nlemadim (DCRQMPF7A9)
```

Switching to this certificate:

```bash
codesign --force --deep \
    --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" \
    /tmp/GaimerDesktop.app
```

### The complication

The first attempt to sign with the Apple Development certificate failed:

```
resource fork, Finder information, or similar detritus not allowed
```

The project lives in `~/Documents`, which is synced by iCloud Drive. iCloud adds extended attributes (`com.apple.FinderInfo`, resource forks) to files. `codesign` rejects any binary that carries these attributes — they make the code signature non-reproducible.

The existing workaround was already in place: `ditto --norsrc` strips extended attributes when copying the app bundle. But we had to enforce the order — build unsigned, strip with ditto, THEN sign:

```bash
dotnet build -p:EnableCodeSigning=false
ditto --norsrc .../GaimerDesktop.app /tmp/GaimerDesktop.app
codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" /tmp/GaimerDesktop.app
```

**Layer 6 status:** Stable signing identity established. TCC can now track the app across builds. But still denied.

---

## Layer 7: Missing Entitlements + Wrong Location

### Three final problems stacked on top of each other

By this point, we had a correctly signed app with a stable identity. The user had granted Screen Recording permission. And TCC still said no. Three more issues were hiding:

### Problem 7a: No entitlements embedded

When you build a .NET MAUI Mac Catalyst app normally, MSBuild handles code signing and embeds the project's `Entitlements.plist` into the app signature. But we were building with `EnableCodeSigning=false` and signing manually afterward. Manual `codesign` does not automatically pick up the entitlements file.

Without entitlements, macOS treats the app as having zero capabilities. No Screen Recording. No camera. No nothing. The app might as well be unsigned.

Fix: pass `--entitlements` explicitly:

```bash
codesign --force --deep \
    --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" \
    --entitlements /tmp/GaimerDesktop.entitlements \
    /tmp/GaimerDesktop.app
```

### Problem 7b: Deployed to /tmp/

We had been deploying to `/tmp/GaimerDesktop.app` to avoid the iCloud extended attributes issue. But macOS System Settings could not properly display an app running from `/tmp/`. In the Screen Recording permission list, the app showed up as `(null)` — no name, no icon. Some users reported that TCC refused to grant permissions to apps in non-standard locations entirely.

Fix: deploy to `/Applications/` instead:

```bash
rm -rf /Applications/GaimerDesktop.app
ditto --norsrc .../GaimerDesktop.app /Applications/GaimerDesktop.app
codesign ... /Applications/GaimerDesktop.app
```

### Problem 7c: Stale TCC entries

All the previous failed attempts — ad-hoc builds, unsigned builds, builds in different locations — had left a trail of conflicting entries in the TCC database. macOS was confused about which "GaimerDesktop" had which permissions.

Fix: reset the TCC database for our bundle ID:

```bash
tccutil reset ScreenCapture com.5dof.gaimer
```

This cleared all Screen Recording permission entries for our app, forcing a clean grant from System Settings.

**Layer 7 status:** Entitlements embedded. App in /Applications/. TCC database clean. One more try.

---

## The Breakthrough

After all seven layers of fixes — the Swift bridge, the deadlock prevention, the DLL resolver, the TCC permission flow, the Sequoia adaptation, the stable signing identity, the entitlements and deployment location — we launched the app one more time.

```
[NativeMethods] Successfully loaded GaimerScreenCapture framework
[NativeMethods] SCK is available: True
[WindowCaptureService] Found window 1684: app=Chess, title=Game 1 | Tony Nlemadim - Computer (White to Move)
[WindowCaptureService] captureImage returned: 2562x2326
[WindowCaptureService] PNG encoded: 4458463 bytes
[WindowCaptureService] SCK capture SUCCESS — 4458463 bytes
```

The chess board was visible in the preview. Full Retina resolution (2562x2326 — a 2x capture of the window). Every piece, every square, every GPU-rendered SceneKit element captured perfectly. The same board that was invisible to the legacy API — just desktop wallpaper where the game should have been — was now crystal clear.

4.4 megabytes of PNG. Seven layers of platform debugging. One chess board.

---

## Lessons Learned

These are not theoretical observations. Each one cost real debugging time. They are documented here so the next developer who hits these walls does not have to discover them from scratch.

### 1. .NET has no runtime framework resolution

`[DllImport("MyFramework")]` does NOT search the app bundle's `Contents/Frameworks/` directory. .NET calls `dlopen()` with the bare name, which searches system paths only. You must use `NativeLibrary.SetDllImportResolver` to manually construct the path to your framework binary inside the app bundle.

This is not a bug. It is a design decision — .NET does not assume macOS framework conventions. But it is also not documented anywhere in the MAUI guides.

### 2. macOS Sequoia TCC is fundamentally stricter

Three changes that break existing patterns:

- **Ad-hoc signatures get new identities per build.** TCC cannot track an app whose identity changes every time it is compiled. Use a stable Apple Development certificate.
- **`CGRequestScreenCaptureAccess()` no longer shows dialogs.** It silently opens System Settings. If you call it every launch, System Settings opens every launch. Remove the call and guide users manually.
- **Stale TCC entries from old identities persist and conflict.** `tccutil reset ScreenCapture <bundle-id>` is the only way to clean them up.

### 3. @MainActor can deadlock P/Invoke

If C# blocks on a `TaskCompletionSource` waiting for a native callback, and Swift dispatches that callback on `@MainActor`, the main thread is deadlocked: C# holds it, Swift needs it. Use `Task.detached` to ensure the callback fires on a background thread.

This is subtle because it only manifests when the C# caller blocks synchronously. If the caller uses `await`, the continuation may run on a thread pool thread and the deadlock does not occur. But `await` is not always possible — especially in timer callbacks, event handlers, or initialization code.

### 4. Entitlements must be explicitly embedded

When bypassing MSBuild's code signing (`EnableCodeSigning=false`) and signing manually with `codesign`, the entitlements file is NOT automatically included. You must pass `--entitlements path/to/Entitlements.plist` explicitly. Without entitlements, macOS treats the app as having zero capabilities.

### 5. iCloud ~/Documents corrupts code signing

Files in iCloud-synced directories acquire extended attributes (`com.apple.FinderInfo`, resource forks) that `codesign` rejects with "resource fork, Finder information, or similar detritus not allowed." Use `ditto --norsrc` to copy the app bundle to a clean location before signing. This strips all extended attributes.

### 6. `tccutil reset` is your friend

When TCC gets confused by multiple signing identities — which is inevitable during iterative development — reset the permission database for your bundle ID:

```bash
tccutil reset ScreenCapture com.5dof.gaimer
```

Then re-grant permission through System Settings. This is faster and more reliable than trying to diagnose which identity has which permission.

### 7. Deploy to /Applications/

TCC and System Settings expect apps in standard locations. An app running from `/tmp/` may show up as `(null)` in the permission list, and some permission grants may silently fail. `/Applications/` is the expected location for user-installed apps. Use it for development builds too.

---

## The Final Build Command

After seven layers of debugging, this is the command that produces a working, permission-grantable, screen-capture-capable build:

```bash
# Build without MSBuild signing (we sign manually for entitlements control)
dotnet build src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj \
    -f net8.0-maccatalyst \
    -p:EnableCodeSigning=false

# Strip iCloud extended attributes and deploy to /Applications/
rm -rf /Applications/GaimerDesktop.app
ditto --norsrc \
    src/GaimerDesktop/GaimerDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/GaimerDesktop.app \
    /Applications/GaimerDesktop.app

# Sign with stable identity + entitlements
codesign --force --deep \
    --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" \
    --entitlements /tmp/GaimerDesktop.entitlements \
    /Applications/GaimerDesktop.app

# Launch
open /Applications/GaimerDesktop.app
```

---

## Architecture Diagram

For reference, the complete capture pipeline after all fixes:

```
┌─────────────────────────────────────────────────────────┐
│  C# (.NET MAUI Mac Catalyst)                            │
│                                                         │
│  WindowCaptureService.cs                                │
│    │                                                    │
│    │  [DllImport("GaimerScreenCapture")]                │
│    │  resolved via NativeLibrary.SetDllImportResolver    │
│    │                                                    │
│    ├─► sck_is_available()                               │
│    │     └─► returns true                               │
│    │                                                    │
│    └─► sck_capture_window(windowID, w, h, callback)     │
│          │                                              │
└──────────┼──────────────────────────────────────────────┘
           │  P/Invoke boundary (C calling convention)
           │
┌──────────┼──────────────────────────────────────────────┐
│  Swift (GaimerScreenCapture.framework)                  │
│          │                                              │
│          ▼                                              │
│    Task.detached(priority: .userInitiated)               │
│          │                                              │
│          ▼                                              │
│    SCShareableContent.current                           │
│          │  find window by CGWindowID                    │
│          ▼                                              │
│    SCScreenshotManager.captureImage(                    │
│        contentFilter: window,                           │
│        configuration: size + scaleFactor                │
│    )                                                    │
│          │                                              │
│          ▼                                              │
│    CGImage → CGImageDestination → PNG Data              │
│          │                                              │
│          ▼                                              │
│    callback(pngPointer, length)                         │
│          │  fires on background thread (no deadlock)    │
└──────────┼──────────────────────────────────────────────┘
           │
           ▼
┌─────────────────────────────────────────────────────────┐
│  C# callback                                            │
│    Marshal.Copy(ptr, bytes, 0, len)                     │
│    TaskCompletionSource.SetResult(bytes)                │
│          │                                              │
│          ▼                                              │
│    BrainEventRouter.OnScreenCapture(pngBytes)           │
│          │                                              │
│          ├──► Timeline Feed (UI update)                 │
│          ├──► AI Vision API (frame analysis)            │
│          └──► Proactive Alert (if interesting)          │
└─────────────────────────────────────────────────────────┘
```

---

## Closing Thoughts

Platform interop is archaeology. Each layer you excavate reveals the next layer beneath it. The Swift bridge was the easy part — well-documented, straightforward. The real work was everything after: runtime library loading, thread safety across language boundaries, OS security policies that change between major versions, code signing semantics that interact with cloud file sync, and deployment location requirements that are enforced but not documented.

None of these seven problems would have been found by reading documentation. Each one was discovered by running the code and watching it fail. The only way through was forward — fix the current error, discover the next one, repeat.

Seven layers. One chess board. Worth every debug cycle.

---

*This document is part of the GAIMER Desktop "build in public" series. For the technical SDK reference, see [SCREENCAPTUREKIT_SDK.md](./SCREENCAPTUREKIT_SDK.md). For the capture architecture spec, see [SCREEN_CAPTURE_ARCHITECTURE.md](./SCREEN_CAPTURE_ARCHITECTURE.md).*
