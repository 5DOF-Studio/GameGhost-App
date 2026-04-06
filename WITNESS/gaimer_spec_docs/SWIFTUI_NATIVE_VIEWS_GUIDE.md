# SwiftUI Native Views in MAUI — Integration Guide

**Date:** 2026-03-14
**Status:** Scaffold ready, PoC not yet built

---

## Overview

Design SwiftUI views in Xcode. Build as a Mac Catalyst xcframework. Inject into MAUI views via P/Invoke. No rewrite — the exact SwiftUI design runs natively.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│ Xcode Project (SwiftUI)                                  │
│                                                          │
│   ConnectorCard.swift ─── your full SwiftUI design       │
│   ToolStatusCard.swift                                   │
│   GhostMessageCard.swift                                 │
│        │                                                 │
│        ▼                                                 │
│   UIHostingController ── wraps SwiftUI as UIView         │
│        │                                                 │
│        ▼                                                 │
│   @_cdecl("create_xxx") ── exports UIView pointer        │
│   @_cdecl("set_xxx")    ── exports state setters         │
│   @_cdecl("on_xxx")     ── exports callback registration │
│                                                          │
│   build-xcframework.sh → GaimerViews.xcframework         │
└────────────────────┬────────────────────────────────────┘
                     │ copy to Platforms/MacCatalyst/
                     ▼
┌─────────────────────────────────────────────────────────┐
│ MAUI Project                                             │
│                                                          │
│   NativeMethods.cs ── DllImportResolver loads framework  │
│   GaimerViewsNativeMethods.cs ── P/Invoke declarations   │
│   NativeSwiftUIHandler.cs ── MAUI handler embeds UIView  │
│        │                                                 │
│        ▼                                                 │
│   <views:NativeSwiftUIView /> in XAML                    │
│   ViewModel binds data → P/Invoke setters → SwiftUI      │
└─────────────────────────────────────────────────────────┘
```

## Why This Works

| Concern | Answer |
|---------|--------|
| SwiftUI on Catalyst? | Yes — Mac Catalyst uses iOS SwiftUI runtime, which has `UIHostingController` |
| Why not macOS SwiftUI? | macOS uses `NSHostingView` which doesn't exist in Catalyst's runtime. That's the GaimerGhostMode constraint |
| Performance? | Native — SwiftUI renders via Metal, no XAML translation layer |
| Animations? | All SwiftUI animations work: `.matchedGeometryEffect`, `.spring()`, `.transition()` |
| SF Symbols? | Yes — full access to iOS/Catalyst symbol library |

## Constraint: What CAN'T Use This

- **Ghost mode floating panel** — needs `NSPanel` (AppKit), must stay pure AppKit
- **Anything needing to float outside the MAUI window** — AppKit territory
- **Windows** — SwiftUI is Apple-only; these views would need XAML equivalents for Windows

## Step-by-Step Guide

### Step 1: Design in Xcode

Create a new Xcode project or use the scaffold at:
```
src/WitnessDesktop/NativeHelpers/GaimerViews/
```

Design your SwiftUI views normally. Use `@ObservedObject` state objects so C# can push data:

```swift
class CardState: ObservableObject {
    @Published var title: String = ""
    @Published var isActive: Bool = false
}

struct MyCard: View {
    @ObservedObject var state: CardState

    var body: some View {
        // Your full design here — gradients, animations, SF Symbols, everything
        HStack {
            Circle().fill(state.isActive ? .green : .gray).frame(width: 10, height: 10)
            Text(state.title).font(.system(size: 14, weight: .semibold))
        }
        .padding(16)
        .background(RoundedRectangle(cornerRadius: 14).fill(.ultraThinMaterial))
    }
}
```

### Step 2: Export via @_cdecl

Create a global state instance and export factory + setter functions:

```swift
private let cardState = CardState()

// Factory — returns UIView pointer
@_cdecl("gaimer_views_create_my_card")
public func createMyCard() -> UnsafeMutableRawPointer {
    let hosting = UIHostingController(rootView: MyCard(state: cardState))
    hosting.view.backgroundColor = .clear
    hosting.view.frame = CGRect(x: 0, y: 0, width: 300, height: 56)
    return Unmanaged.passRetained(hosting.view).toOpaque()
}

// Setter — C# calls this to push data
@_cdecl("gaimer_views_set_my_card_state")
public func setMyCardState(titlePtr: UnsafePointer<CChar>, isActive: Bool) {
    let title = String(cString: titlePtr)
    DispatchQueue.main.async {
        cardState.title = title
        cardState.isActive = isActive
    }
}

// Cleanup
@_cdecl("gaimer_views_release")
public func releaseView(viewPtr: UnsafeMutableRawPointer) {
    Unmanaged<UIView>.fromOpaque(viewPtr).release()
}
```

### Step 3: Build the xcframework

```bash
cd src/WitnessDesktop/NativeHelpers/GaimerViews
./build-xcframework.sh
```

This builds for Mac Catalyst and copies to `Platforms/MacCatalyst/GaimerViews.xcframework`.

### Step 4: Register in NativeMethods.cs

Add to the DllImportResolver in `NativeMethods.cs`:

```csharp
private const string ViewsLibName = "GaimerViews";

private static IntPtr ResolveDllImport(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
{
    // ... existing resolvers ...

    if (libraryName == ViewsLibName)
        return ResolveFramework("GaimerViews");

    return IntPtr.Zero;
}
```

### Step 5: P/Invoke declarations

Create `Platforms/MacCatalyst/GaimerViewsNativeMethods.cs`:

```csharp
using System.Runtime.InteropServices;

namespace WitnessDesktop.Platforms.MacCatalyst;

internal static class GaimerViewsNativeMethods
{
    // Factory
    [DllImport("GaimerViews", CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr gaimer_views_create_my_card();

    // State setter
    [DllImport("GaimerViews", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gaimer_views_set_my_card_state(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        [MarshalAs(UnmanagedType.U1)] bool isActive);

    // Cleanup
    [DllImport("GaimerViews", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gaimer_views_release(IntPtr viewPtr);

    // Tap callback
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void TapCallback();

    [DllImport("GaimerViews", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void gaimer_views_set_connector_tap_callback(TapCallback callback);
}
```

### Step 6: MAUI Handler

Create a custom MAUI handler that embeds the native UIView:

```csharp
#if MACCATALYST
using UIKit;

namespace WitnessDesktop.Controls;

public class NativeSwiftUIView : ContentView
{
    private IntPtr _nativeViewPtr;

    public void LoadView(Func<IntPtr> factory)
    {
        _nativeViewPtr = factory();
        if (_nativeViewPtr == IntPtr.Zero) return;

        var nativeView = ObjCRuntime.Runtime.GetNSObject<UIView>(_nativeViewPtr);
        if (nativeView == null) return;

        // Wrap in a MAUI-compatible view
        var platformView = new Microsoft.Maui.Platform.ContentView();
        platformView.AddSubview(nativeView);

        // Set constraints or frame
        nativeView.TranslatesAutoresizingMaskIntoConstraints = false;
        NSLayoutConstraint.ActivateConstraints(new[] {
            nativeView.LeadingAnchor.ConstraintEqualTo(platformView.LeadingAnchor),
            nativeView.TrailingAnchor.ConstraintEqualTo(platformView.TrailingAnchor),
            nativeView.TopAnchor.ConstraintEqualTo(platformView.TopAnchor),
            nativeView.BottomAnchor.ConstraintEqualTo(platformView.BottomAnchor),
        });

        Content = platformView.ToView(); // Convert to MAUI view
    }

    ~NativeSwiftUIView()
    {
        if (_nativeViewPtr != IntPtr.Zero)
            GaimerViewsNativeMethods.gaimer_views_release(_nativeViewPtr);
    }
}
#endif
```

### Step 7: Use in XAML

```xml
<!-- Embed the SwiftUI view in any MAUI layout -->
<controls:NativeSwiftUIView x:Name="ConnectorCardView" />
```

```csharp
// In code-behind or ViewModel
ConnectorCardView.LoadView(GaimerViewsNativeMethods.gaimer_views_create_my_card);

// Push data updates
GaimerViewsNativeMethods.gaimer_views_set_my_card_state("Chess.app", true);
```

## Data Flow Patterns

### Push (C# → SwiftUI)
```
ViewModel property change
  → P/Invoke setter call (gaimer_views_set_xxx)
  → Swift @_cdecl function
  → DispatchQueue.main.async
  → ObservableObject @Published property
  → SwiftUI re-renders automatically
```

### Pull (SwiftUI → C#)
```
SwiftUI .onTapGesture / .onChange
  → Call stored @convention(c) function pointer
  → P/Invoke callback fires in C#
  → MAUI event handler / command
```

### Complex Data
For complex objects (arrays, nested models), use JSON:
```swift
@_cdecl("gaimer_views_set_card_data_json")
public func setCardDataJson(jsonPtr: UnsafePointer<CChar>) {
    let json = String(cString: jsonPtr)
    if let data = json.data(using: .utf8),
       let decoded = try? JSONDecoder().decode(CardData.self, from: data) {
        DispatchQueue.main.async {
            cardState.update(from: decoded)
        }
    }
}
```

## Files in Scaffold

```
src/WitnessDesktop/NativeHelpers/GaimerViews/
├── Package.swift                              ← Swift Package manifest (Mac Catalyst target)
├── build-xcframework.sh                       ← Build + copy to MAUI project
└── Sources/GaimerViews/
    └── GaimerViews.swift                      ← PoC views + @_cdecl exports
```

## How to Get New Views Into the App

1. **Design** in Xcode (can be a separate Xcode project, or open the Package.swift directly)
2. **Add** new SwiftUI views + state objects + @_cdecl exports to `Sources/GaimerViews/`
3. **Build**: `./build-xcframework.sh`
4. **Declare** P/Invoke functions in `GaimerViewsNativeMethods.cs`
5. **Embed** via `NativeSwiftUIView.LoadView()` in the MAUI view that needs it
6. **Wire** data flow: ViewModel → P/Invoke setters for push, callback registration for pull

## Naming Convention

| Swift export | C# P/Invoke | Purpose |
|---|---|---|
| `gaimer_views_create_xxx` | `gaimer_views_create_xxx()` → `IntPtr` | Factory (returns UIView) |
| `gaimer_views_set_xxx` | `gaimer_views_set_xxx(...)` | Push data to SwiftUI |
| `gaimer_views_on_xxx` | `gaimer_views_on_xxx(callback)` | Register event callback |
| `gaimer_views_release` | `gaimer_views_release(ptr)` | Dispose native view |
