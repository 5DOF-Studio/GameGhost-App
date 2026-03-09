# Ghost Mode Technical Infrastructure Report

**Last Updated:** 2026-02-27
**Status:** Implemented (macOS / Mac Catalyst)
**Scope:** End-to-end overlay system — Swift native layer, C# interop bridge, build pipeline

---

## 1. Architecture Overview

Ghost Mode replaces the full MAUI window with a transparent native floating panel that sits above all windows, including fullscreen games. The architecture is a three-layer sandwich: C# ViewModel logic at the top, P/Invoke bridge in the middle, and pure AppKit Swift code at the bottom.

```
+-------------------------------------------------------------------+
|                    C# / .NET MAUI Layer                           |
|                                                                   |
|  MainViewModel                  IGhostModeService (interface)     |
|  - ToggleFabAsync()             - EnterGhostModeAsync()           |
|  - UpdateFabVoiceState()        - ExitGhostModeAsync()            |
|  - TextReceived handler         - ShowCard() / DismissCard()      |
|  - ConnectionState handler      - SetFabState() / SetAgentImage() |
|        |                              |                           |
|        v                              v                           |
|  MacGhostModeService (platform impl)                              |
|  - GCHandle pinned callbacks                                      |
|  - Dispose ordering (destroy panel, then free handles)            |
+-------------------+-----------------------------------------------+
                    |
                    | P/Invoke (DllImport / @_cdecl)
                    | 14 exported C functions
                    |
+-------------------v-----------------------------------------------+
|                   NativeMethods.cs                                |
|  - DllImportResolver (single per assembly)                        |
|  - dlopen/dlerror diagnostics                                     |
|  - Resolves GaimerGhostMode + GaimerScreenCapture                 |
+-------------------+-----------------------------------------------+
                    |
                    | dlopen from Contents/Frameworks/
                    |
+-------------------v-----------------------------------------------+
|              GaimerGhostMode.xcframework (Swift)                  |
|                                                                   |
|  GaimerGhostMode.swift          GhostPanel.swift                  |
|  - 14 @_cdecl exports           - NSPanel subclass                |
|  - Singleton sharedPanel         - .borderless .nonactivatingPanel |
|  - Singleton sharedState         - .floating level                 |
|  - runOnMainSync helper          - .canJoinAllSpaces              |
|  - Host window hide/restore      - .fullScreenAuxiliary           |
|        |                              |                           |
|        v                              v                           |
|  GhostPanelContentView.swift    FabView.swift                     |
|  - Root NSView (delegate pattern)  - 56pt circular CALayer button |
|  - Click-through hitTest           - Yellow glow ring (connected) |
|  - Card show/hide animations       - Circular hitTest             |
|  - Auto-dismiss timers             - Press animations             |
|        |                                                          |
|        v                                                          |
|  EventCardView.swift                                              |
|  - Variant 1: Voice (avatar + animated dots)                      |
|  - Variant 2: Text (title pill + body)                            |
|  - Variant 3: TextWithImage (text + image below)                  |
|  - Tap-to-dismiss with race guard                                 |
+-------------------------------------------------------------------+
```

### Data Flow — Showing an Event Card

```
C# TextReceived handler
  -> MainViewModel checks _ghostModeService.IsGhostModeActive
  -> MacGhostModeService.ShowCard(FabCardVariant.Text, "AI INSIGHT", text, null)
  -> GhostModeNativeMethods.ghost_panel_show_card(2, "AI INSIGHT", text, null)
       [P/Invoke marshals strings as LPUTF8Str]
  -> @_cdecl ghostPanelShowCard()   [Swift]
       [Copies string pointers to Swift String IMMEDIATELY]
  -> DispatchQueue.main.async { sharedState.cardVariant = 2 }
  -> GhostPanelState.cardVariant didSet -> delegate.stateDidChange()
  -> GhostPanelContentView.showCard()
       [Creates EventCardNSView, slide-in animation, starts 8s auto-dismiss timer]
```

---

## 2. Why Pure AppKit (No SwiftUI)

The ghost panel uses pure AppKit (NSPanel, NSView, CALayer) instead of SwiftUI. This is not a preference -- it is a hard technical constraint.

### The Problem

Mac Catalyst apps run in an iOS compatibility environment. When you `import SwiftUI` in a framework loaded into a Mac Catalyst process, the runtime loads the **iOS variant** of SwiftUI, not the macOS variant. The iOS SwiftUI variant does not include `NSHostingView` (which is macOS-only). Attempting to use `NSHostingView` produces:

```
error: 'NSHostingView' is unavailable in Mac Catalyst
```

Even if you could get it to compile, the iOS SwiftUI variant lacks AppKit integration points entirely. There is no workaround within the SwiftUI framework.

### The Solution

All UI is built with raw AppKit:
- `NSPanel` for the floating window
- `NSView` subclasses for content (no `NSHostingView`)
- `CAShapeLayer` / `CALayer` for drawing (circles, pills, icons)
- `NSAnimationContext` for slide-in/fade-out animations
- `NSTextField(labelWithString:)` for text labels
- `NSImageView` for card images
- `DispatchSourceTimer` for animation ticks (animated dots) and auto-dismiss

### What This Means for Future Development

Any new UI in the ghost panel must also be pure AppKit. You cannot introduce SwiftUI views, `@State`, `@Published`, `ObservableObject`, or any SwiftUI property wrappers. The `GhostPanelState` class uses a manual delegate pattern (`GhostPanelStateDelegate`) instead of SwiftUI's `@Published` / `@ObservedObject` bindings.

---

## 3. Platform Tag Bridge (vtool)

### The Problem

The GaimerGhostMode framework imports AppKit (`NSPanel`, `NSWindow`, `NSApplication`). AppKit headers are not available in the Mac Catalyst SDK. Therefore, the framework must be compiled for **plain macOS**, not Mac Catalyst.

However, when a Mac Catalyst process calls `dlopen()` on a Mach-O binary, the loader checks the platform tag embedded in the binary's `LC_BUILD_VERSION` load command. If the tag says "macOS" (platform 1) but the process is running as Mac Catalyst (platform 6), `dlopen` rejects it with:

```
dlopen failed: no compatible arch found
```

AppKit is actually available at runtime inside Catalyst processes (UIKit uses it internally). The only barrier is the platform tag in the Mach-O header.

### The Pipeline

The `build-xcframework.sh` script uses a three-stage pipeline:

**Stage 1: Build for macOS**
```bash
xcodebuild build \
    -scheme GaimerGhostMode \
    -destination "generic/platform=macOS" \
    -configuration Release \
    BUILD_LIBRARY_FOR_DISTRIBUTION=YES
```

This produces a universal (arm64 + x86_64) dylib tagged as macOS platform.

**Stage 2: Create xcframework**
```bash
xcodebuild -create-xcframework \
    -framework GaimerGhostMode.framework \
    -output GaimerGhostMode.xcframework
```

This must happen BEFORE retagging because `xcodebuild -create-xcframework` copies binaries internally, which would overwrite any earlier retag.

**Stage 3: Retag with vtool**
```bash
# For each architecture slice:
lipo "$FINAL_BINARY" -thin arm64 -output "GaimerGhostMode_arm64"
vtool -set-build-version maccatalyst 16.0 26.2 \
    -replace \
    -output "GaimerGhostMode_arm64_retagged" \
    "GaimerGhostMode_arm64"

# Repeat for x86_64, then recombine:
lipo -create "GaimerGhostMode_arm64" "GaimerGhostMode_x86_64" \
    -output "$FINAL_BINARY"
```

The `vtool -set-build-version maccatalyst 16.0 26.2 -replace` command rewrites the `LC_BUILD_VERSION` load command to say "Mac Catalyst" instead of "macOS". The `16.0` is the minimum deployment target, `26.2` is the SDK version.

### Why vtool Must Run AFTER xcodebuild -create-xcframework

This is critical: `xcodebuild -create-xcframework` copies the `.framework` directory into the xcframework structure, creating fresh copies of the binary. Any vtool retagging done before this step would be lost because xcframework creation overwrites the binary with the original macOS-tagged copy.

The script retags the **final binary** at its destination inside the xcframework directory that has already been copied to the MAUI project location (`Platforms/MacCatalyst/GaimerGhostMode.xcframework/`).

### Verification

After retagging, the script verifies:
```bash
vtool -show-build "$FINAL_BINARY" | grep -A2 "platform"
```

Expected output should show `MACCATALYST` as the platform, not `MACOS`.

---

## 4. DllImportResolver

### The Constraint

`NativeLibrary.SetDllImportResolver` can only be called **once per assembly**. The GaimerDesktop assembly has two native frameworks (GaimerScreenCapture and GaimerGhostMode), so a single resolver must handle both.

### NativeMethods.cs Static Constructor

The resolver is registered in the static constructor of `NativeMethods`:

```csharp
static NativeMethods()
{
    NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, ResolveDllImport);
}
```

The resolver dispatches by library name:
```csharp
private static IntPtr ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
{
    if (libraryName == "GaimerScreenCapture")
        return ResolveFramework("GaimerScreenCapture");
    if (libraryName == "GaimerGhostMode")
        return ResolveFramework("GaimerGhostMode");
    return IntPtr.Zero; // default resolution
}
```

### GhostModeNativeMethods Static Constructor Chain

`GhostModeNativeMethods` might be accessed before `NativeMethods`. If that happens, the DllImportResolver would not exist yet, and the runtime would throw `DllNotFoundException` for "GaimerGhostMode". To prevent this, `GhostModeNativeMethods` forces the `NativeMethods` static constructor to run first:

```csharp
static GhostModeNativeMethods()
{
    RuntimeHelpers.RunClassConstructor(typeof(NativeMethods).TypeHandle);
}
```

This guarantees the resolver is registered regardless of which class is touched first by DI container initialization or P/Invoke resolution.

### Resolution Path

The `ResolveFramework` method tries two paths inside the app bundle:

1. Flat path: `<AppBundle>/Contents/Frameworks/GaimerGhostMode.framework/GaimerGhostMode`
2. Versioned path: `<AppBundle>/Contents/Frameworks/GaimerGhostMode.framework/Versions/A/GaimerGhostMode`

### Diagnostic dlopen/dlerror

When `NativeLibrary.TryLoad` fails, the resolver calls raw `dlopen` and `dlerror` via P/Invoke to `/usr/lib/libSystem.B.dylib` to get the actual error message. This is essential for diagnosing platform tag mismatches, codesigning issues, and missing dependencies. The error messages go to `Console.WriteLine` and appear in the macOS Console.app or Xcode debug output.

Common dlopen errors and their meaning:
- `"no compatible arch found"` -- vtool retagging was not applied or was overwritten
- `"code signature invalid"` -- framework needs re-codesigning after binary modification
- `"image not found"` -- CopyGhostModeFramework MSBuild target did not run

---

## 5. Window Management

### The Problem with UIWindow.Hidden

Mac Catalyst apps have a dual window stack: a UIKit `UIWindow` inside an AppKit `NSWindow` (specifically a `UINSWindow`). Setting `UIWindow.Hidden = true` only hides the UIKit content layer. The underlying `NSWindow` chrome remains visible -- you get an empty dark window with a title bar floating on screen.

### The Solution: NSWindow.orderOut

The ghost panel framework provides two functions to manage the host window at the AppKit level:

**Hiding the host window:**
```swift
@_cdecl("ghost_panel_hide_host_window")
public func ghostPanelHideHostWindow() {
    DispatchQueue.main.async {
        for window in NSApplication.shared.windows {
            if window !== sharedPanel && window.isVisible &&
               (window.title.contains("Gaimer") || window.className.contains("UINSWindow")) {
                hiddenHostWindow = window
                window.orderOut(nil)
                return
            }
        }
    }
}
```

The function iterates `NSApplication.shared.windows` looking for the MAUI window (identified by title containing "Gaimer" or class name containing "UINSWindow"), stores a reference to it, and calls `orderOut(nil)` to fully remove it from the screen.

**Restoring the host window:**
```swift
@_cdecl("ghost_panel_show_host_window")
public func ghostPanelShowHostWindow() {
    DispatchQueue.main.async {
        if let window = hiddenHostWindow {
            window.makeKeyAndOrderFront(nil)
            hiddenHostWindow = nil
        } else {
            // Fallback: find any UINSWindow
            for window in NSApplication.shared.windows {
                if window !== sharedPanel && window.className.contains("UINSWindow") {
                    window.makeKeyAndOrderFront(nil)
                    return
                }
            }
        }
    }
}
```

The fallback path handles the case where `hiddenHostWindow` was released (e.g., if the app recreated its window while in ghost mode).

### Enter/Exit Sequence

**Enter ghost mode** (MacGhostModeService.EnterGhostModeAsync):
1. `ghost_panel_hide_host_window()` -- hide the MAUI NSWindow
2. `ghost_panel_show()` -- bring the ghost panel to front

**Exit ghost mode** (MacGhostModeService.ExitGhostModeAsync):
1. `ghost_panel_hide()` -- hide the ghost panel
2. `ghost_panel_show_host_window()` -- restore the MAUI NSWindow

Order matters. Hiding the host before showing the panel prevents a visual flash. Hiding the panel before restoring the host prevents a frame where both are visible.

### Auto-Exit on Disconnect

The `ConnectionStateChanged` handler in `MainViewModel` automatically exits ghost mode when the connection drops:

```csharp
if (state is ConnectionState.Disconnected or ConnectionState.Error or ConnectionState.Disconnecting)
{
    if (_ghostModeService.IsGhostModeActive)
        _ = _ghostModeService.ExitGhostModeAsync();
    IsFabActive = false;
}
```

This prevents the user from being stuck in ghost mode (no MAUI window, no connection) with no way to get back to the main UI.

---

## 6. DispatchQueue Threading

### Why Not @MainActor

Swift's `@MainActor` annotation works with Swift's structured concurrency (`async`/`await`). But `@_cdecl` functions are called synchronously from C# P/Invoke. If the C# caller is blocking (e.g., `Task.Wait()` or `Task.Result`), and the `@_cdecl` function tries to schedule work via `@MainActor`, the behavior is unpredictable -- it may deadlock or crash depending on which thread the P/Invoke lands on.

All `@_cdecl` functions use `DispatchQueue.main.async` for UI operations:

```swift
@_cdecl("ghost_panel_show")
public func ghostPanelShow() {
    DispatchQueue.main.async {
        sharedPanel?.orderFront(nil)
    }
}
```

### The main.sync Deadlock

`ghost_panel_create` needs synchronous execution (the caller needs to know immediately if creation succeeded). It uses `DispatchQueue.main.sync` -- but if called from the main thread, this deadlocks. The `runOnMainSync` helper prevents this:

```swift
private func runOnMainSync(_ block: () -> Void) {
    if Thread.isMainThread {
        block()
    } else {
        DispatchQueue.main.sync(execute: block)
    }
}
```

`MacGhostModeService` is constructed by the DI container. If the DI container runs on the main thread (which MAUI's `ConfigureServices` does), calling `ghost_panel_create` directly would deadlock on `DispatchQueue.main.sync`. The `Thread.isMainThread` guard runs the block directly in that case.

### String Pointer Lifetime

`@_cdecl` functions receive C string parameters as `UnsafePointer<CChar>`. These pointers are managed by .NET's P/Invoke marshaller and are only valid for the duration of the synchronous `@_cdecl` call. If the string is captured inside a `DispatchQueue.main.async` closure, the pointer may be freed before the closure executes.

Solution: convert to Swift `String` immediately, before dispatching:

```swift
@_cdecl("ghost_panel_set_agent_image")
public func ghostPanelSetAgentImage(pathPtr: UnsafePointer<CChar>) {
    let path = String(cString: pathPtr)  // Copy NOW, before async dispatch
    DispatchQueue.main.async {
        sharedState?.agentImagePath = path  // Use the copied String
    }
}
```

This pattern applies to all `@_cdecl` functions that accept string parameters: `ghost_panel_set_agent_image`, `ghost_panel_show_card` (title, text, imagePath).

---

## 7. Click-Through Hit Testing

The ghost panel covers a large screen area (340pt wide, full screen height) but most of it is transparent. Clicks on transparent areas must pass through to the game window below.

### GhostPanelContentView.hitTest

```swift
override func hitTest(_ point: NSPoint) -> NSView? {
    let result = super.hitTest(point)
    if result == nil || result === self {
        return nil  // Pass through: nothing hit, or root view hit
    }
    return result  // An actual subview (FAB or card) was hit
}
```

Returning `nil` from `hitTest` tells AppKit this view does not handle the click, passing it through to the window below. The check `result === self` ensures that clicks on the root view's transparent background also pass through.

### FabButtonView.hitTest (Circular)

The FAB is circular, but NSView frames are rectangular. Without a custom `hitTest`, clicking in the corners of the FAB's frame (outside the circle) would register as a hit. The FAB uses distance-from-center testing:

```swift
override func hitTest(_ point: NSPoint) -> NSView? {
    let local = convert(point, from: superview)
    let center = NSPoint(x: bounds.midX, y: bounds.midY)
    let dx = local.x - center.x
    let dy = local.y - center.y
    let radius = bounds.width / 2
    if dx * dx + dy * dy <= radius * radius {
        return self
    }
    return nil
}
```

### Event Cards

Event cards (`EventCardNSView`) do NOT override `hitTest`. Their rectangular frame is the hit target. Any tap on a visible card triggers `mouseUp`, which calls `safeDismiss()`.

### NSPanel Configuration

The panel itself is configured to not steal focus:
- `styleMask: [.borderless, .nonactivatingPanel]` -- no title bar, does not activate when clicked
- `canBecomeKey: true` -- allows receiving key events when explicitly made key
- `canBecomeMain: false` -- never becomes the main window
- `hidesOnDeactivate: false` -- stays visible when app loses focus

---

## 8. Event Card System

### Three Variants

| Variant | Int32 Value | Content | Auto-Dismiss |
|---------|-------------|---------|--------------|
| Voice | 1 | Agent avatar + "is talking..." + animated dots | NO |
| Text | 2 | Title pill (cyan) + text body (white, up to 6 lines) | 8 seconds |
| TextWithImage | 3 | Title pill + text body + image (max 200pt height) | 8 seconds |

Variant 0 means "no card" -- setting `cardVariant = 0` dismisses any visible card.

### Card Lifecycle

1. **C# calls `ShowCard`** -- marshals variant int + nullable strings across P/Invoke
2. **Swift stores card data** in `GhostPanelState` properties, then sets `cardVariant` (triggers `didSet`)
3. **`stateDidChange` calls `showCard()`** -- removes any existing card, creates a new `EventCardNSView`
4. **Slide-in animation** -- card starts offscreen (x = bounds.width + cardWidth), alpha 0, animates to final position over 0.25s with ease-in-ease-out timing
5. **Auto-dismiss timer** starts (for variant 2 and 3 only) -- 8 second `DispatchSourceTimer`
6. **Dismiss triggers** -- either user tap, auto-dismiss timer, or explicit `ghost_panel_dismiss_card` from C#
7. **Fade-out animation** -- 0.2s alpha-to-zero, then `removeFromSuperview()`
8. **Callback fires** -- `onCardDismiss?()` invokes the C function pointer, which fires `CardDismissed` event in C#
9. **MainViewModel resets** `FabCardVariant = FabCardVariant.None`

### Voice Card Exemption

Voice cards (variant 1) are NOT auto-dismissed because voice activity is ongoing. The C# side manages their lifecycle:
- `UpdateFabVoiceState()` shows voice card when `ActivityVolume > 0.01f`
- When volume drops to zero, C# calls `DismissCard()` explicitly

### Auto-Dismiss Timer (DispatchSourceTimer)

```swift
private func scheduleAutoDismiss(seconds: Double) {
    cancelAutoDismiss()
    let timer = DispatchSource.makeTimerSource(queue: .main)
    timer.schedule(deadline: .now() + seconds)
    timer.setEventHandler { [weak self] in
        self?.dismissCard()
    }
    timer.resume()
    autoDismissTimer = timer
}
```

`DispatchSourceTimer` is used instead of `Timer` because:
- It runs on a specified GCD queue (`.main`) without needing a RunLoop
- It has deterministic cancellation via `.cancel()`
- It does not retain the target (uses `[weak self]`)

### Dismissed Flag (Race Prevention)

`EventCardNSView` has a `dismissed` flag:

```swift
private var dismissed = false

private func safeDismiss() {
    guard !dismissed else { return }
    dismissed = true
    onDismiss?()
}
```

This prevents a race where the user taps the card at the exact moment the auto-dismiss timer fires. Without the flag, `onDismiss` could fire twice, causing the callback to reach C# twice and potentially corrupting state.

### Animated Dots (Voice Card)

The `AnimatedDotsView` uses a `DispatchSourceTimer` repeating every 0.4s, cycling through three dots (opacity 1.0 for the active dot, 0.3 for inactive). The timer automatically starts when the view is added to a window (`viewDidMoveToWindow`) and stops when removed, preventing leaked timers.

---

## 9. Callback Bridge

### C# Side: GCHandle Pinning

C# delegates are managed objects that the garbage collector can move or collect. When passing a function pointer to native code, the delegate must be pinned to prevent GC collection while native code holds the pointer.

```csharp
// Store as field to prevent GC of the delegate itself
_fabTapCallback = OnFabTapped;
_cardDismissCallback = OnCardDismissed;

// Pin with GCHandle to prevent relocation
_fabTapHandle = GCHandle.Alloc(_fabTapCallback);
_cardDismissHandle = GCHandle.Alloc(_cardDismissCallback);

// Register with native layer
GhostModeNativeMethods.ghost_panel_set_fab_tap_callback(_fabTapCallback);
GhostModeNativeMethods.ghost_panel_set_card_dismiss_callback(_cardDismissCallback);
```

Three things keep the callback alive:
1. The field reference (`_fabTapCallback`) prevents the delegate from being collected
2. The `GCHandle` prevents the delegate from being relocated in memory
3. Both must remain valid for the lifetime of the native panel

### Swift Side: @convention(c) Storage

```swift
var fabTapCallback: (@convention(c) () -> Void)?
var cardDismissCallback: (@convention(c) () -> Void)?
```

These are stored as module-level globals (not instance properties) because the `@_cdecl` functions are free functions, not methods.

The callback registration stores the raw C function pointer and wraps it in a Swift closure for the state delegate:

```swift
fabTapCallback = callback
DispatchQueue.main.async {
    sharedState?.onFabTap = {
        fabTapCallback?()
    }
}
```

### Dispose Ordering

`MacGhostModeService.Dispose` must destroy the native panel BEFORE freeing the GCHandle pins:

```csharp
// 1. Destroy panel first (unregisters callback pointers on native side)
GhostModeNativeMethods.ghost_panel_destroy();

// 2. NOW safe to free pinned handles
_fabTapHandle.Free();
_cardDismissHandle.Free();
```

If the handles were freed first, a callback could fire between the `Free()` and `destroy()` calls (e.g., an auto-dismiss timer expiring), invoking a function pointer to freed memory. This would cause a segfault or corrupted stack.

The native `ghost_panel_destroy` sets all callback references to nil:
```swift
fabTapCallback = nil
cardDismissCallback = nil
```

---

## 10. Build Pipeline

### build-xcframework.sh

The script is located at:
```
src/GaimerDesktop/NativeHelpers/GaimerGhostMode/build-xcframework.sh
```

**Step-by-step:**

1. **Clean** -- removes `.build-xcframework/` directory
2. **xcodebuild build** -- compiles the Swift Package for macOS (Release, BUILD_LIBRARY_FOR_DISTRIBUTION=YES)
3. **Locate built framework** -- searches derived data for `GaimerGhostMode.framework`; if only a bare dylib is found, creates a framework directory structure manually with an Info.plist
4. **xcodebuild -create-xcframework** -- wraps the framework in an xcframework
5. **Copy to MAUI project** -- copies xcframework to `Platforms/MacCatalyst/`
6. **vtool retag** -- retags each architecture slice from macOS to Mac Catalyst (see Section 3)
7. **Verify** -- prints platform tag and binary info

### Package.swift

```swift
let package = Package(
    name: "GaimerGhostMode",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "GaimerGhostMode", type: .dynamic, targets: ["GaimerGhostMode"])
    ],
    targets: [
        .target(
            name: "GaimerGhostMode",
            path: "Sources/GaimerGhostMode",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("CoreGraphics")
            ]
        )
    ]
)
```

Key: `type: .dynamic` produces a `.dylib` / `.framework` instead of a static library. This is required because `@_cdecl` exports need to be in a dynamic library for `dlopen` to find them.

### CopyGhostModeFramework MSBuild Target

In `GaimerDesktop.csproj`:

```xml
<Target Name="CopyGhostModeFramework"
        AfterTargets="_ExpandNativeReferences"
        Condition="$(TargetFramework.Contains('maccatalyst'))">
    <PropertyGroup>
        <_GhostModeSrc>$(MSBuildProjectDirectory)/Platforms/MacCatalyst/GaimerGhostMode.xcframework/macos-arm64_x86_64/GaimerGhostMode.framework</_GhostModeSrc>
        <_GhostModeDest>$(AppBundleDir)/Contents/Frameworks/GaimerGhostMode.framework</_GhostModeDest>
    </PropertyGroup>
    <Exec Command="rm -rf &quot;$(_GhostModeDest)&quot;" />
    <Exec Command="mkdir -p &quot;$(AppBundleDir)/Contents/Frameworks&quot;" />
    <Exec Command="cp -R &quot;$(_GhostModeSrc)&quot; &quot;$(_GhostModeDest)&quot;" />
</Target>
```

**Why not NativeReference?** The `<NativeReference>` item in MSBuild expects xcframework slices tagged for the correct platform. Since our xcframework is retagged macOS-to-Catalyst (a non-standard configuration), `NativeReference` rejects it as incompatible. The manual copy target bypasses this validation entirely and places the framework directly in `Contents/Frameworks/` where `dlopen` finds it.

**AfterTargets="_ExpandNativeReferences"** ensures the app bundle directory exists before the copy runs.

**Note:** The xcframework directory inside the MAUI project is `macos-arm64_x86_64/` (the original platform name from before retagging). The binary inside it IS retagged to Mac Catalyst, but the directory name retains the original macOS label. This is cosmetic and does not affect loading.

### Codesigning

After `dotnet build`, the app bundle at `bin/Debug/.../GaimerDesktop.app` must be codesigned with the `--deep` flag to cover all nested frameworks:

```bash
codesign --force --deep \
    --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" \
    --entitlements /tmp/GaimerDesktop.entitlements \
    /Applications/GaimerDesktop.app
```

The `--deep` flag recursively signs `Contents/Frameworks/GaimerGhostMode.framework/`. Without it, the framework's code signature (invalidated by vtool retagging) would cause dlopen to fail with "code signature invalid".

---

## 11. Known Constraints and Gotchas

### Build Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| `DllNotFoundException: GaimerGhostMode` | Framework not in app bundle | Run `build-xcframework.sh`, rebuild .NET project |
| `dlopen: no compatible arch found` | vtool retag didn't run or was overwritten | Re-run `build-xcframework.sh` (retag is last step) |
| `dlopen: code signature invalid` | Binary modified after codesigning | Re-codesign with `--force --deep` |
| `EntryPointNotFoundException: ghost_panel_create` | Symbol not exported | Verify `@_cdecl("ghost_panel_create")` in Swift, check `nm -gU` on binary |
| xcframework slice directory says `macos-arm64_x86_64` | Expected behavior -- directory name reflects original build platform, not retagged platform | No action needed |
| Build error "no rule to process file" | Package.swift not in expected location | Check directory structure matches `Sources/GaimerGhostMode/` |

### Runtime Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Ghost panel doesn't appear | `ghost_panel_show` called before `ghost_panel_create` | Check Console.app for `[GaimerGhostMode]` logs |
| MAUI window visible behind ghost panel | `ghost_panel_hide_host_window` didn't find the window | Window title or class name changed; update the search logic |
| Empty dark window with title bar remains | Using UIWindow.Hidden instead of NSWindow.orderOut | Must use the native `ghost_panel_hide_host_window` function |
| Deadlock on startup | `ghost_panel_create` called from main thread without `runOnMainSync` guard | Ensure the Thread.isMainThread check is in place |
| Crash in callback after dispose | GCHandle freed before native panel destroyed | Verify dispose ordering: destroy panel first, then free handles |
| String garbage in card text | String pointer captured in async closure without copying | Convert `UnsafePointer<CChar>` to `String` before `DispatchQueue.main.async` |
| Card dismiss fires twice | Auto-dismiss timer and user tap race | The `dismissed` flag in EventCardNSView prevents this |
| Ghost panel hidden when app loses focus | `hidesOnDeactivate` is true | Ensure `hidesOnDeactivate = false` in GhostPanel init |
| Clicks don't pass through to game | hitTest returning non-nil for transparent areas | Verify GhostPanelContentView.hitTest returns nil for `result === self` |
| FAB clicks register in corners (outside circle) | FabButtonView.hitTest not using circular test | Verify distance-from-center calculation |

### Diagnostic Commands

Check if framework is in the app bundle:
```bash
ls -la /Applications/GaimerDesktop.app/Contents/Frameworks/GaimerGhostMode.framework/
```

Check platform tag:
```bash
vtool -show-build /Applications/GaimerDesktop.app/Contents/Frameworks/GaimerGhostMode.framework/GaimerGhostMode
```

Check exported symbols:
```bash
nm -gU /Applications/GaimerDesktop.app/Contents/Frameworks/GaimerGhostMode.framework/GaimerGhostMode | grep ghost_panel
```

Check architecture:
```bash
file /Applications/GaimerDesktop.app/Contents/Frameworks/GaimerGhostMode.framework/GaimerGhostMode
```

Monitor runtime logs:
```bash
log stream --predicate 'message contains "GaimerGhostMode"' --info
```

---

## 12. Future Upgrade Path

### Adding Windows Support

The ghost mode architecture is macOS-specific. A Windows implementation would need:

1. **New native helper** -- A Win32/WinUI 3 project producing a DLL with C-callable exports matching the same 14-function API surface.
2. **Window type** -- Use a Win32 layered window (`WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST`) or a WinUI 3 `DesktopAcrylic` window for the overlay.
3. **Platform service** -- Create `WindowsGhostModeService : IGhostModeService` in `Platforms/Windows/` with the same DllImport pattern.
4. **No vtool needed** -- Windows DLLs don't have platform tags. A standard DLL can be loaded by any Windows process.
5. **Click-through** -- Use `WS_EX_TRANSPARENT` extended style or handle `WM_NCHITTEST` returning `HTTRANSPARENT`.
6. **DllImportResolver** -- Extend `NativeMethods.ResolveDllImport` to handle the Windows library name, or use a separate resolver if on a different assembly.

See also: `GAIMER/GameGhost/gaimer_spec_docs/WINDOWS_OVERLAY_IMPLEMENTATION_PLAN.md` for additional Windows overlay design notes.

### Adding New Card Types

To add a new card variant (e.g., variant 4: MapCard with coordinates):

1. **Swift: EventCardView.swift** -- Add `case 4` to the switch in `init`, implement `buildMapCard(...)` method.
2. **Swift: GaimerGhostMode.swift** -- No changes needed (variant is passed as `Int32`, new values just work).
3. **C# Models** -- Add `Map = 4` to `FabCardVariant` enum.
4. **C# ViewModel** -- Add logic in the appropriate event handler to call `ShowCard(FabCardVariant.Map, ...)`.
5. **Auto-dismiss** -- Decide whether the new variant auto-dismisses. If not, add it to the voice exemption check: `if state.cardVariant != 1 && state.cardVariant != 4`.

The `ghost_panel_show_card` API already accepts optional title, text, and imagePath strings. If the new card type needs additional data, you have two options:
- **Encode in existing fields** -- e.g., put JSON in the text parameter
- **Add new @_cdecl export** -- e.g., `ghost_panel_set_card_metadata(key, value)` called before `show_card`

### Drag-to-Reposition

The ghost panel currently has a fixed position (right edge, full height). To add drag-to-reposition:

1. **FAB drag handling** -- Override `mouseDragged(with:)` in `FabButtonView` to track mouse delta and update the panel's frame origin via `sharedPanel?.setFrameOrigin(...)`.
2. **Position persistence** -- Store the last position in `UserDefaults` and restore it in `ghost_panel_create`.
3. **Edge snapping** -- After drag ends, snap to the nearest screen edge (left or right) to prevent the FAB from being left in the middle of the screen.
4. **Card position** -- The card layout in `GhostPanelContentView.layout()` currently assumes right-aligned. If the panel can be on the left, the card should flip to the opposite side.

### Panel Resizing

The panel size is set once at creation. To support dynamic resizing (e.g., adapting to different screen sizes or multi-monitor setups):

1. Use `NSScreen.screens` notifications to detect display changes
2. Call `ghost_panel_set_size` and `ghost_panel_set_position` from C# when the active display changes
3. Consider `NSWindow.didChangeScreenNotification` for automatic repositioning

---

## File Reference

| File | Language | Purpose |
|------|----------|---------|
| `NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GaimerGhostMode.swift` | Swift | 14 @_cdecl exports, singleton state, host window management |
| `NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GhostPanel.swift` | Swift | NSPanel subclass with overlay configuration |
| `NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GhostPanelContentView.swift` | Swift | Root view, state delegate, card show/hide, click-through |
| `NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/FabView.swift` | Swift | FAB button with CALayer rendering, circular hit test |
| `NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/EventCardView.swift` | Swift | Three card variants, animated dots, dismiss guard |
| `NativeHelpers/GaimerGhostMode/Package.swift` | Swift | Swift Package Manager manifest |
| `NativeHelpers/GaimerGhostMode/build-xcframework.sh` | Bash | Build + vtool retag + copy pipeline |
| `Platforms/MacCatalyst/GhostModeNativeMethods.cs` | C# | P/Invoke declarations for all 14 ghost_panel_* functions |
| `Platforms/MacCatalyst/MacGhostModeService.cs` | C# | IGhostModeService implementation, GCHandle lifecycle |
| `Platforms/MacCatalyst/NativeMethods.cs` | C# | DllImportResolver, dlopen diagnostics |
| `Services/IGhostModeService.cs` | C# | Cross-platform interface contract |
| `Models/FabCardVariant.cs` | C# | Enum: None, Voice, Text, TextWithImage |
| `ViewModels/MainViewModel.cs` | C# | Ghost mode integration in ToggleFabAsync, event handlers |
| `GaimerDesktop.csproj` | MSBuild | CopyGhostModeFramework target |
