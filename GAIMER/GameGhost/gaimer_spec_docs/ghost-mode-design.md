# Ghost Mode — FAB Overlay Design Document

## Overview

Ghost Mode is the in-game companion surface for Gaimer. A Floating Action Button (FAB) on MainPage toggles between Dashboard mode (full MAUI UI) and Ghost mode (transparent floating panel over the game). The user can see their game unobstructed while the FAB and event cards float on top.

## Decision Log

### Phase 1: FAB on MainPage (Completed)

Replaced the bottom-right Connect button with a circular FAB that shows the agent's portrait image. The FAB:

- Sits in the MainPage footer bar where the Connect button was
- Uses `PortraitImage` with circular clip for a profile-pic appearance
- Shows a yellow glow ring when connected (`IsFabEnabled`)
- Dims to 0.3 opacity when disconnected
- Toggles `IsFabActive` on tap (shows X icon when active)

**Files created:**
- `Models/FabCardVariant.cs` — Enum: None, Voice, Text, TextWithImage
- `Views/FabOverlayView.xaml` + `.xaml.cs` — FAB button ContentView with AttachViewModel pattern

**Files modified:**
- `ViewModels/MainViewModel.cs` — Added IsFabActive, FabCardVariant, computed properties (IsFabEnabled, IsFabCardVisible, ShowVoiceCard, ShowTextCard, ShowCardImage), ToggleFab command, UpdateFabVoiceState(), FAB card triggering in TextReceived, reset on disconnect
- `MainPage.xaml` — Replaced bottom-right Connect button with FabOverlayView, added dim overlay BoxView
- `MainPage.xaml.cs` — Wired FabOverlay.AttachViewModel/DetachViewModel
- `AppShell.xaml.cs` — Disabled dev launcher (production flow only)

### Phase 2: Ghost Mode (Planned)

**Problem:** MAUI on Mac Catalyst cannot make its own window transparent. The `NSWindow` behind the MAUI `UIWindow` requires private API access (`valueForKey("nsWindow")`), which Apple has warned may break in future OS updates and risks App Store rejection.

**Decision:** Build a native Swift xcframework (same pattern as `GaimerScreenCapture.xcframework`) that creates a separate `NSPanel` for the ghost mode overlay. This uses only public Apple APIs.

**Rejected alternatives:**
- `valueForKey("nsWindow")` — Private API, Apple warned it will break
- `MacCatalystWindowHelper` — Proof-of-concept, unmaintained, uses same private API
- Second MAUI window — MAUI windows can't be transparent on Catalyst
- NSStatusItem/popover — Not enough control over positioning

## Ghost Mode Architecture

### Two-Mode Design

| Mode | Window | What's Visible |
|------|--------|---------------|
| Dashboard | MAUI window (normal) | Full app UI, FAB in footer |
| Ghost | MAUI window hidden, native NSPanel shown | Only FAB + event cards, game visible behind |

### Platform Strategy

| Platform | Ghost Mode Implementation |
|----------|--------------------------|
| macOS | Native NSPanel xcframework (public API) |
| Windows | Win32 layered window / WinUI transparent (official API) |

Both platforms share the same ViewModel logic (`IsFabActive`, `FabCardVariant`, `ToggleFab`). A shared `IGhostModeService` interface abstracts the platform-specific window manipulation.

### NSPanel xcframework Responsibilities

1. Create a transparent, always-on-top `NSPanel` with `level = .floating`
2. Render the FAB button (circular agent portrait, yellow glow ring)
3. Render event cards (voice variant, text variant, text+image variant)
4. Accept data from MAUI via P/Invoke (agent image path, card text, card variant)
5. Send tap events back to MAUI via callback (FAB tapped, card dismissed)

### Toggle Flow

```
User taps FAB → ToggleFab() in MainViewModel
  → IGhostModeService.EnterGhostMode()
    → macOS: hide MAUI window, show NSPanel with FAB
    → Windows: make MAUI window transparent, keep FAB visible

AI event arrives → FabCardVariant set in MainViewModel
  → IGhostModeService.ShowCard(variant, title, text, image?)
    → macOS: render SwiftUI card in NSPanel
    → Windows: render MAUI card (window is already transparent)

User taps X → ToggleFab() in MainViewModel
  → IGhostModeService.ExitGhostMode()
    → macOS: hide NSPanel, show MAUI window
    → Windows: restore MAUI window opacity
```

### UX Details

- **FAB label:** "Double tap for Ghost Mode" (tooltip/long-press hint)
- **Ghost mode FAB:** Shows X icon, tapping exits ghost mode
- **Event cards:** Auto-dismiss after timeout, tap to dismiss early
- **Voice card:** Agent avatar + "is talking..." + audio bars
- **Text card:** Title pill + text body + optional image

## References

- `Platforms/MacCatalyst/GaimerScreenCapture.xcframework` — Existing native framework pattern
- `Views/GaimerHudView.xaml.cs` — AttachViewModel/auto-dismiss timer pattern
- [NSPanel Apple Documentation](https://developer.apple.com/documentation/appkit/nspanel)
- [Floating Panel in SwiftUI](https://cindori.com/developer/floating-panel)
