# Phase 04: Ghost Mode -- Native Floating Overlay - Research

**Researched:** 2026-02-26
**Domain:** macOS NSPanel overlay, Swift/AppKit interop, Mac Catalyst native framework integration
**Confidence:** MEDIUM (architectural approach verified, implementation details from training + web research)

## Summary

Ghost Mode requires a native floating overlay (NSPanel) that sits above the game window while the MAUI UI is hidden. The primary challenge is that Mac Catalyst apps do NOT have direct access to AppKit, but NSPanel is an AppKit class. Research identified two viable approaches and recommends the macOS-native xcframework approach, which follows the existing GaimerScreenCapture pattern most closely.

The existing project already has a proven pattern: a Swift Package built as a dynamic framework, exported via @_cdecl C-callable functions, consumed from C# via DllImport/NativeLibrary.TryLoad. The ghost mode framework should follow this same pattern but build for macOS (not Mac Catalyst) so it can import AppKit and create NSPanel instances. AppKit is already loaded in the Mac Catalyst process (for UIKit-to-NSView bridging), so macOS-native dylibs that reference AppKit symbols can load successfully at runtime.

For click-through transparency, NSPanel must be configured with `isOpaque = false`, `backgroundColor = .clear`, and the contentView's `hitTest(_:)` must be overridden to return `nil` for transparent areas (passing clicks through to the game) while returning the view for opaque interactive elements (FAB button, event cards). The `ignoresMouseEvents` property should NOT be used globally because it would make the entire panel click-through, including the interactive elements.

**Primary recommendation:** Build GaimerGhostMode as a macOS-native Swift Package with `import AppKit`, compiled to xcframework for macOS (not Mac Catalyst). Export @_cdecl functions for show/hide panel, update content, and register callbacks. Load via NativeLibrary.TryLoad, same as GaimerScreenCapture.

## Standard Stack

### Core
| Library/Framework | Version | Purpose | Why Standard |
|-------------------|---------|---------|--------------|
| AppKit (NSPanel) | macOS 13+ | Floating transparent overlay window | Only public API for always-on-top transparent panels |
| SwiftUI | macOS 13+ | Render FAB button and event cards in panel | Modern declarative UI, hosted via NSHostingView |
| NSHostingView | macOS 13+ | Bridge SwiftUI views into NSPanel contentView | Official way to embed SwiftUI in AppKit windows |

### Supporting
| Library/Framework | Version | Purpose | When to Use |
|-------------------|---------|---------|-------------|
| CoreGraphics | macOS 13+ | Screen geometry, positioning | Panel placement calculations |
| Combine | macOS 13+ | Reactive state for card updates | SwiftUI state management inside panel |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| NSPanel (public API) | NSWindow valueForKey (private API) | Private API risks App Store rejection, Apple explicitly warned |
| macOS-native xcframework | Mac Catalyst xcframework + objc_msgSend | Runtime ObjC messaging loses type safety, harder to debug |
| macOS-native xcframework | AppKit plugin bundle | Different packaging than existing pattern, harder to integrate with MAUI NativeReference |
| SwiftUI in panel | Raw CALayer/CoreAnimation | Far more code, no declarative UI benefits |

**Installation / Integration:**
```xml
<!-- GaimerDesktop.csproj -- add alongside existing GaimerScreenCapture -->
<ItemGroup Condition="$(TargetFramework.Contains('maccatalyst'))">
    <NativeReference Include="Platforms\MacCatalyst\GaimerGhostMode.xcframework">
        <Kind>Framework</Kind>
        <SmartLink>True</SmartLink>
    </NativeReference>
</ItemGroup>
```

## Architecture Patterns

### Recommended Project Structure
```
src/GaimerDesktop/NativeHelpers/
    GaimerScreenCapture/          # Existing (Mac Catalyst target)
    GaimerGhostMode/              # NEW (macOS native target)
        Package.swift
        Sources/GaimerGhostMode/
            GaimerGhostMode.swift          # @_cdecl exports (C API surface)
            GhostPanel.swift               # NSPanel subclass
            GhostPanelContentView.swift    # SwiftUI root view
            FabView.swift                  # SwiftUI FAB button
            EventCardView.swift            # SwiftUI event card variants
        build-xcframework.sh

src/GaimerDesktop/GaimerDesktop/
    Services/
        IGhostModeService.cs               # Cross-platform interface
    Platforms/
        MacCatalyst/
            GaimerGhostMode.xcframework/   # Pre-built, checked into repo
            GhostModeNativeMethods.cs      # DllImport declarations
            MacGhostModeService.cs         # IGhostModeService implementation
        Windows/
            WinGhostModeService.cs         # Stub/future implementation
```

### Pattern 1: macOS-Native xcframework with @_cdecl (CRITICAL)

**What:** Build the ghost mode Swift code targeting macOS (NOT Mac Catalyst) so it can `import AppKit`. Export C-callable functions via @_cdecl. The MAUI Mac Catalyst app loads this macOS-native dylib at runtime.

**Why this works:** Mac Catalyst processes already have AppKit loaded (Apple bridges UIKit views to NSViews internally). A macOS-native dylib that references AppKit symbols can load successfully because those symbols are already resolved in the process. Xcode may emit a linker warning about platform mismatch, but the dylib loads and functions correctly.

**Key difference from GaimerScreenCapture:** The existing screen capture framework is built for Mac Catalyst (`destination "generic/platform=macOS,variant=Mac Catalyst"`). The ghost mode framework must be built for native macOS (`destination "generic/platform=macOS"`) because AppKit is only available in the macOS SDK, not the Mac Catalyst SDK.

**Build script modification:**
```bash
# GaimerScreenCapture builds for Mac Catalyst:
xcodebuild build \
    -scheme "GaimerScreenCapture" \
    -destination "generic/platform=macOS,variant=Mac Catalyst" \
    ...

# GaimerGhostMode builds for native macOS:
xcodebuild build \
    -scheme "GaimerGhostMode" \
    -destination "generic/platform=macOS" \
    ...
```

**Package.swift for ghost mode:**
```swift
// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "GaimerGhostMode",
    platforms: [
        .macOS(.v13)  // macOS target, NOT macCatalyst
    ],
    products: [
        .library(
            name: "GaimerGhostMode",
            type: .dynamic,
            targets: ["GaimerGhostMode"]
        )
    ],
    targets: [
        .target(
            name: "GaimerGhostMode",
            path: "Sources/GaimerGhostMode",
            linkerSettings: [
                .linkedFramework("AppKit"),
                .linkedFramework("SwiftUI"),
                .linkedFramework("CoreGraphics")
            ]
        )
    ]
)
```

### Pattern 2: NSPanel Floating Overlay Configuration

**What:** Create a transparent, always-on-top NSPanel that sits above all windows including fullscreen games.

**NSPanel subclass configuration:**
```swift
import AppKit
import SwiftUI

class GhostPanel: NSPanel {
    init() {
        super.init(
            contentRect: NSRect(x: 0, y: 0, width: 300, height: 400),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )

        // Transparent background -- game shows through
        self.isOpaque = false
        self.backgroundColor = .clear
        self.hasShadow = false

        // Always on top, even above other app windows
        self.level = .floating  // or .screenSaver for above fullscreen
        self.isFloatingPanel = true

        // Don't steal focus from the game
        self.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]

        // Don't show in Window menu / Mission Control
        self.isExcludedFromWindowsMenu = true

        // Keep panel alive after close
        self.isReleasedWhenClosed = false

        // Allow panel to receive key events when clicked
        // but don't activate the app
        self.hidesOnDeactivate = false
    }

    // Override to allow panel to receive clicks without activating app
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}
```

### Pattern 3: Click-Through Transparency

**What:** Transparent areas of the panel pass clicks to the game below. Only the FAB button and event cards intercept mouse events.

**How it works:** Override `hitTest(_:)` on the content view. Return `nil` for clicks in transparent areas (they fall through to the window below). Return the actual subview for clicks on opaque interactive elements.

```swift
class ClickThroughHostingView<Content: View>: NSHostingView<Content> {
    override func hitTest(_ point: NSPoint) -> NSView? {
        // Let the normal hit testing find the deepest view
        guard let hitView = super.hitTest(point) else {
            return nil  // Click falls through to game
        }

        // If hit testing returns THIS view (the hosting container),
        // it means the click landed on a transparent area
        if hitView === self {
            return nil  // Fall through
        }

        // Click landed on an actual SwiftUI control (FAB, card, etc.)
        return hitView
    }
}
```

**CRITICAL:** Do NOT set `ignoresMouseEvents = true` on the panel. That would make the ENTIRE panel click-through, including the FAB and cards. Instead, use the hitTest override pattern above for selective click-through.

**CRITICAL:** Do NOT set `wantsLayer = true` on the content view unless necessary. Setting wantsLayer on the root view can disable the automatic click-through behavior of transparent windows (NSBorderlessWindowMask). If you must use layers for SwiftUI content, the hitTest override approach becomes essential.

### Pattern 4: MAUI Window Hide/Show

**What:** Hide the MAUI UIWindow when entering ghost mode, restore it when exiting.

**From C# (Mac Catalyst):** Use UIApplication.SharedApplication.KeyWindow or scene-based window access.
```csharp
// Hide MAUI window
#if MACCATALYST
var window = UIKit.UIApplication.SharedApplication.KeyWindow;
window.Hidden = true;  // Hides the UIWindow (and its backing NSWindow)
#endif

// Show MAUI window
#if MACCATALYST
window.Hidden = false;
window.MakeKeyAndVisible();
#endif
```

**Alternative via native code:** The ghost mode xcframework can also hide/show the MAUI window by accessing NSApplication.shared.windows and finding the main window.

### Pattern 5: @_cdecl C API Surface

**What:** The complete set of C-callable functions exported by the xcframework.

```swift
// --- Lifecycle ---
@_cdecl("ghost_panel_create")
public func ghostPanelCreate() -> Bool { ... }

@_cdecl("ghost_panel_destroy")
public func ghostPanelDestroy() { ... }

// --- Visibility ---
@_cdecl("ghost_panel_show")
public func ghostPanelShow() { ... }

@_cdecl("ghost_panel_hide")
public func ghostPanelHide() { ... }

// --- FAB Configuration ---
@_cdecl("ghost_panel_set_agent_image")
public func ghostPanelSetAgentImage(_ pathPtr: UnsafePointer<CChar>) { ... }

@_cdecl("ghost_panel_set_fab_active")
public func ghostPanelSetFabActive(_ active: Bool) { ... }

@_cdecl("ghost_panel_set_fab_connected")
public func ghostPanelSetFabConnected(_ connected: Bool) { ... }

// --- Event Cards ---
@_cdecl("ghost_panel_show_card")
public func ghostPanelShowCard(
    _ variant: Int32,           // 0=None, 1=Voice, 2=Text, 3=TextWithImage
    _ titlePtr: UnsafePointer<CChar>?,
    _ textPtr: UnsafePointer<CChar>?,
    _ imagePathPtr: UnsafePointer<CChar>?
) { ... }

@_cdecl("ghost_panel_dismiss_card")
public func ghostPanelDismissCard() { ... }

// --- Callbacks ---
@_cdecl("ghost_panel_set_fab_tap_callback")
public func ghostPanelSetFabTapCallback(
    _ callback: @convention(c) () -> Void
) { ... }

@_cdecl("ghost_panel_set_card_dismiss_callback")
public func ghostPanelSetCardDismissCallback(
    _ callback: @convention(c) () -> Void
) { ... }

// --- Positioning ---
@_cdecl("ghost_panel_set_position")
public func ghostPanelSetPosition(_ x: Double, _ y: Double) { ... }

@_cdecl("ghost_panel_set_size")
public func ghostPanelSetSize(_ width: Double, _ height: Double) { ... }
```

### Anti-Patterns to Avoid
- **NSWindow valueForKey("nsWindow"):** Private API. Apple explicitly warned it will break. The project already rejected this approach.
- **ignoresMouseEvents = true on panel:** Makes entire panel click-through. Use hitTest override instead.
- **Building for Mac Catalyst with `import AppKit`:** Compiler error. AppKit is not available in the Mac Catalyst SDK. Must build for macOS.
- **Using SCStream for UI rendering:** SCStream is for screen capture. NSPanel + SwiftUI is the correct approach for overlay UI.
- **Dispatching to @MainActor from @_cdecl:** Can deadlock if C# side blocks with Task.Wait(). Use DispatchQueue.main.async instead (lesson from Phase 03).
- **Single xcframework with both macOS and maccatalyst slices:** The NativeReference system picks the maccatalyst slice for a Catalyst app. To load a macOS-native dylib, it may need to be loaded explicitly via NativeLibrary.TryLoad with a direct path, or use a macOS-only xcframework.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Floating panel window | Custom CALayer overlay | NSPanel with .floating level | NSPanel handles window ordering, focus, activation correctly |
| SwiftUI-in-AppKit hosting | Manual NSView hierarchy | NSHostingView | Apple's official bridge, handles layout, events, accessibility |
| Click-through transparency | Mouse event forwarding system | hitTest override returning nil | macOS window server handles event routing; hitTest is the standard hook |
| Cross-process window ordering | CGWindowList manipulation | NSPanel.level = .floating | NSPanel.level is the public API for window ordering |
| Auto-dismiss timers | Custom NSTimer management | SwiftUI .task + Task.sleep | SwiftUI lifecycle handles cancellation automatically |

**Key insight:** The problem looks like "draw some circles on the screen" but it's actually window management, focus management, event routing, and process coordination. NSPanel + AppKit handles all of these correctly. Hand-rolling any part leads to subtle bugs with window ordering, focus stealing, and event routing.

## Common Pitfalls

### Pitfall 1: Platform Mismatch in xcframework
**What goes wrong:** Building the ghost mode framework for Mac Catalyst results in compiler error because `import AppKit` is not available on Mac Catalyst.
**Why it happens:** The existing GaimerScreenCapture is built for Mac Catalyst. Copying its build script directly fails.
**How to avoid:** Build for macOS (`-destination "generic/platform=macOS"`) instead of Mac Catalyst. This produces a macOS-native dylib that can import AppKit.
**Warning signs:** Compiler error "No such module 'AppKit'" during build.

### Pitfall 2: xcframework Slice Selection
**What goes wrong:** MAUI's NativeReference system selects the maccatalyst slice of an xcframework for a Mac Catalyst app. If the xcframework only has a macOS slice, NativeReference may fail or produce warnings.
**Why it happens:** NativeReference is designed to pick platform-matching slices.
**How to avoid:** Two approaches: (a) Use a macOS-only xcframework and resolve via NativeLibrary.SetDllImportResolver with explicit path to the framework inside Contents/Frameworks/, or (b) Build the xcframework with a macOS variant and accept the linker warning about platform mismatch. The existing DllImportResolver pattern in NativeMethods.cs already handles custom framework resolution.
**Warning signs:** "linking against dylib built for macOS, but linking in client built for Mac Catalyst" warning.

### Pitfall 3: Deadlock on Main Thread
**What goes wrong:** @_cdecl function dispatches to MainActor/main thread, C# side calls with Task.Wait() blocking the main thread. Deadlock.
**Why it happens:** NSPanel creation and manipulation MUST happen on the main thread. If the C# caller is also on the main thread and blocks, deadlock occurs.
**How to avoid:** For panel operations that must happen on the main thread, use `DispatchQueue.main.async` (fire-and-forget) rather than dispatching and waiting. The @_cdecl functions should return immediately. For operations that need a result, use the callback pattern (same as sck_capture_window).
**Warning signs:** App freezes when entering ghost mode.

### Pitfall 4: Panel Steals Focus from Game
**What goes wrong:** Clicking the FAB button causes the MAUI app to activate, pulling the game to background.
**Why it happens:** Default NSWindow behavior activates the owning application on click.
**How to avoid:** Use `.nonactivatingPanel` in the styleMask. This is the key NSPanel feature: panels can be clicked without activating the application. Also set `hidesOnDeactivate = false` so the panel stays visible when another app (the game) is active.
**Warning signs:** Game goes behind other windows when FAB is tapped.

### Pitfall 5: Panel Disappears When Game Goes Fullscreen
**What goes wrong:** Full-screen games create their own Space, and the panel is not included.
**Why it happens:** Default window collection behavior doesn't include auxiliary panels in fullscreen Spaces.
**How to avoid:** Set `collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]`. The `.canJoinAllSpaces` makes the panel appear in all Spaces including fullscreen. The `.fullScreenAuxiliary` marks it as compatible with fullscreen Spaces.
**Warning signs:** Panel visible in windowed mode but disappears when game enters fullscreen.

### Pitfall 6: GCHandle Not Pinned for Callbacks
**What goes wrong:** C# callback delegate passed to native code gets garbage collected before the native code invokes it.
**Why it happens:** The .NET GC doesn't know the native code holds a reference to the delegate.
**How to avoid:** Pin the delegate with GCHandle.Alloc for the lifetime of the ghost mode session. Free it when exiting ghost mode. This is the same pattern used in WindowCaptureService for SCK callbacks.
**Warning signs:** Crash with "A call was made on a garbage collected delegate."

### Pitfall 7: SwiftUI Content Not Rendering in NSHostingView
**What goes wrong:** The NSHostingView shows a blank/transparent area instead of SwiftUI content.
**Why it happens:** The NSHostingView's frame is zero, or the SwiftUI view has no intrinsic size, or the panel's contentView was not set correctly.
**How to avoid:** Set the panel's contentView to the NSHostingView AND ensure the SwiftUI root view has explicit sizing (.frame modifier). Also call `hostingView.setFrameSize(panel.contentRect(forFrameRect: panel.frame).size)` after setup.
**Warning signs:** Panel appears but is completely transparent/empty.

## Code Examples

### Example 1: DllImport Declarations (C# Side)
```csharp
// Source: follows existing NativeMethods.cs pattern
internal static class GhostModeNativeMethods
{
    private const string LibName = "GaimerGhostMode";

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void GhostModeCallback();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static extern bool ghost_panel_create();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_destroy();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_show();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_hide();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_agent_image(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_fab_active(
        [MarshalAs(UnmanagedType.U1)] bool active);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_fab_connected(
        [MarshalAs(UnmanagedType.U1)] bool connected);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_show_card(
        int variant,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? title,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? text,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string? imagePath);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_dismiss_card();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_fab_tap_callback(
        GhostModeCallback callback);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_card_dismiss_callback(
        GhostModeCallback callback);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_position(double x, double y);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ghost_panel_set_size(double width, double height);
}
```

### Example 2: DllImportResolver for Ghost Mode Framework
```csharp
// Source: follows existing NativeMethods.cs ResolveDllImport pattern
static GhostModeNativeMethods()
{
    NativeLibrary.SetDllImportResolver(
        typeof(GhostModeNativeMethods).Assembly,
        ResolveDllImport);
}

private static IntPtr ResolveDllImport(
    string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
{
    if (libraryName != "GaimerGhostMode")
        return IntPtr.Zero;

    var appBundle = Foundation.NSBundle.MainBundle.BundlePath;
    var frameworkPath = Path.Combine(
        appBundle, "Contents", "Frameworks",
        "GaimerGhostMode.framework", "GaimerGhostMode");

    if (NativeLibrary.TryLoad(frameworkPath, out var handle))
        return handle;

    // Try versioned path
    var versionedPath = Path.Combine(
        appBundle, "Contents", "Frameworks",
        "GaimerGhostMode.framework", "Versions", "A", "GaimerGhostMode");

    if (NativeLibrary.TryLoad(versionedPath, out handle))
        return handle;

    return IntPtr.Zero;
}
```

### Example 3: IGhostModeService Interface
```csharp
// Source: follows IWindowCaptureService pattern
namespace GaimerDesktop.Services;

public interface IGhostModeService
{
    bool IsGhostModeActive { get; }
    bool IsSupported { get; }

    /// <summary>Enter ghost mode: hide MAUI window, show native panel.</summary>
    Task EnterGhostModeAsync();

    /// <summary>Exit ghost mode: hide native panel, show MAUI window.</summary>
    Task ExitGhostModeAsync();

    /// <summary>Update the agent image shown on the FAB.</summary>
    void SetAgentImage(string imagePath);

    /// <summary>Update FAB state (active/connected).</summary>
    void SetFabState(bool active, bool connected);

    /// <summary>Show an event card in ghost mode.</summary>
    void ShowCard(FabCardVariant variant, string? title, string? text, string? imagePath);

    /// <summary>Dismiss the current event card.</summary>
    void DismissCard();

    /// <summary>Fired when user taps the FAB in ghost mode.</summary>
    event EventHandler? FabTapped;

    /// <summary>Fired when event card is dismissed (tap or timeout).</summary>
    event EventHandler? CardDismissed;
}
```

### Example 4: SwiftUI FAB View (in NSPanel)
```swift
// Source: based on existing FabOverlayView.xaml design, ported to SwiftUI
import SwiftUI

struct FabButton: View {
    let agentImagePath: String?
    let isActive: Bool
    let isConnected: Bool
    let onTap: () -> Void

    var body: some View {
        ZStack {
            // Outer glow ring (yellow when connected)
            if isConnected {
                Circle()
                    .stroke(Color.yellow, lineWidth: 2)
                    .frame(width: 64, height: 64)
                    .shadow(color: .yellow.opacity(0.6), radius: 12)
            }

            // Inner circle
            Circle()
                .fill(Color(hex: "#1E1E2E"))  // BgCard equivalent
                .frame(width: 56, height: 56)

            // Content: agent portrait or X
            if isActive {
                Text("\u{2715}")  // X icon
                    .font(.system(size: 24))
                    .foregroundColor(.white)
            } else if let path = agentImagePath,
                      let nsImage = NSImage(contentsOfFile: path) {
                Image(nsImage: nsImage)
                    .resizable()
                    .aspectRatio(contentMode: .fill)
                    .frame(width: 56, height: 56)
                    .clipShape(Circle())
            }
        }
        .opacity(isConnected ? 1.0 : 0.3)
        .onTapGesture { onTap() }
    }
}
```

### Example 5: NativeLibrary Resolution Note
```
IMPORTANT: The existing NativeMethods.cs registers a DllImportResolver on the
assembly. Since both GhostModeNativeMethods and the existing NativeMethods are
in the SAME assembly, only ONE resolver can be registered per assembly.

Solution: Either:
(a) Extend the existing NativeMethods resolver to handle both library names, OR
(b) Put GhostModeNativeMethods in a different class but use the SAME resolver
    registration (the resolver handles both "GaimerScreenCapture" and
    "GaimerGhostMode" library names).

Option (b) is recommended -- add a case in the existing ResolveDllImport method.
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| NSWindow valueForKey private API | NSPanel in native framework | Always (private API was never safe) | App Store safe, future-proof |
| AppKit plugin bundles (.bundle) | xcframework with @_cdecl | xcframework introduced 2019 | Cleaner build, same DllImport pattern as existing code |
| NSView manual layout for overlay UI | NSHostingView + SwiftUI | SwiftUI matured macOS 13+ | Much less code, declarative, easy state management |
| ignoresMouseEvents toggle | hitTest override for selective click-through | Always available, but widely adopted | Precise control over which areas receive clicks |

**Deprecated/outdated:**
- NSWindow valueForKey("nsWindow"): Apple warned this will break. Already rejected in design doc.
- MacCatalystWindowHelper: Proof-of-concept library, unmaintained, uses same private API.

## Open Questions

1. **xcframework platform slice for macOS in Mac Catalyst NativeReference**
   - What we know: NativeReference picks the maccatalyst slice. A macOS-only xcframework might work because the DllImportResolver bypasses NativeReference slice selection and loads directly from Contents/Frameworks/.
   - What's unclear: Whether MAUI's build system will copy a macOS-only xcframework into Contents/Frameworks/ at all, or if it will be rejected during build.
   - Recommendation: Test the build first. If NativeReference rejects the macOS-only xcframework, fall back to manually copying the framework into the app bundle via a build script (post-build event in .csproj). The DllImportResolver will find it either way.

2. **Window level for fullscreen games**
   - What we know: `.floating` level works above normal windows. `.screenSaver` level works above fullscreen.
   - What's unclear: Whether `.floating` + `.fullScreenAuxiliary` collection behavior is sufficient for games that use macOS fullscreen API vs. games that create their own fullscreen window.
   - Recommendation: Start with `.floating` + `.fullScreenAuxiliary`. Test with fullscreen games. Escalate to `.screenSaver` or CGWindowLevel-based level if needed.

3. **Agent image path resolution**
   - What we know: MAUI bundles images in Resources/Images/ which get compiled into the app bundle. The native code needs the full filesystem path to load them.
   - What's unclear: Exact path to MAUI image resources inside the .app bundle on Mac Catalyst.
   - Recommendation: Resolve image path on C# side using `NSBundle.MainBundle.PathForResource()` and pass the full path string to the native code.

4. **Simultaneous DllImportResolver registration**
   - What we know: NativeLibrary.SetDllImportResolver can only be called once per assembly. Both GaimerScreenCapture and GaimerGhostMode need resolution.
   - What's unclear: Whether the existing resolver silently ignores re-registration or throws.
   - Recommendation: Extend the existing NativeMethods.ResolveDllImport to handle both library names. A single resolver that switches on libraryName is the cleanest approach.

## Sources

### Primary (HIGH confidence)
- Existing project source code: GaimerScreenCapture.swift, NativeMethods.cs, WindowCaptureService.cs, build-xcframework.sh -- verified the exact @_cdecl + DllImport + NativeLibrary.TryLoad pattern
- Ghost mode design doc: GAIMER/GameGhost/gaimer_spec_docs/ghost-mode-design.md -- verified architecture decisions
- PROJECT STATE.md -- verified all prior decisions (NSPanel, rejected private API, IGhostModeService)

### Secondary (MEDIUM confidence)
- [Beyond the Checkbox with Catalyst and AppKit](https://www.highcaffeinecontent.com/blog/20190607-Beyond-the-Checkbox-with-Catalyst-and-AppKit) -- AppKit bundle loading in Catalyst apps, NSPanel spawning
- [Using AppKit in Your Mac Catalyst App](https://sebvidal.com/blog/using-appkit-in-your-mac-catalyst-app/) -- Plugin bundle approach, AppKit runtime availability
- [Floating Panel in SwiftUI](https://www.markusbodner.com/til/2021/02/08/create-a-spotlight/alfred-like-window-on-macos-with-swiftui/) -- NSPanel subclass code, NSHostingView integration
- [NSPanel Documentation](https://developer.apple.com/documentation/appkit/nspanel) -- Official Apple docs (fetched but JS-rendered)
- [Mac Catalyst: Get NSWindow from UIWindow](https://gist.github.com/steipete/30c33740bf0ebc34a0da897cba52fefe) -- Confirmed private API risks
- [Catalyst-AppKit GitHub](https://github.com/mhdhejazi/Catalyst-AppKit) -- Validated bundle-based AppKit access pattern

### Tertiary (LOW confidence)
- Web search results about macOS-native dylib loading in Mac Catalyst -- multiple sources suggest it works with warnings but no authoritative Apple documentation confirms this is supported
- hitTest override for selective click-through -- pattern is well-known but not verified against NSHostingView specifically with SwiftUI content

## Metadata

**Confidence breakdown:**
- Standard stack: MEDIUM -- NSPanel + SwiftUI + NSHostingView is the standard approach, but the macOS-native xcframework loaded in Mac Catalyst is non-standard (works but may emit warnings)
- Architecture: HIGH -- follows exact same pattern as existing GaimerScreenCapture, all @_cdecl + DllImport patterns are proven in this codebase
- C API surface: HIGH -- directly modeled on existing sck_is_available/sck_capture_window pattern
- Click-through: MEDIUM -- hitTest override is standard AppKit pattern, but interaction with NSHostingView needs validation
- Pitfalls: HIGH -- several pitfalls directly observed/resolved in Phase 03 (GCHandle pinning, main thread deadlock, DllImportResolver)
- MAUI window hide/show: MEDIUM -- UIWindow.Hidden is standard UIKit, but edge cases around window restoration need testing

**Research date:** 2026-02-26
**Valid until:** 2026-03-26 (stable APIs, no fast-moving dependencies)
