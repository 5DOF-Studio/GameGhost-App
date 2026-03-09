---
phase: 04-ghost-mode
plan: 01
subsystem: native-overlay
tags: [swift, nspanel, swiftui, xcframework, appkit, ghost-mode]
dependency-graph:
  requires: [03-screen-capture]
  provides: [ghost-mode-xcframework, ghost-panel-cdecl-exports]
  affects: [04-02, 04-03]
tech-stack:
  added: [SwiftUI-overlay, NSPanel]
  patterns: [cdecl-exports, click-through-hosting-view, dispatch-queue-main]
key-files:
  created:
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Package.swift
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GhostPanel.swift
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GhostPanelContentView.swift
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/FabView.swift
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/EventCardView.swift
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/Sources/GaimerGhostMode/GaimerGhostMode.swift
    - src/GaimerDesktop/NativeHelpers/GaimerGhostMode/build-xcframework.sh
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/GaimerGhostMode.xcframework
  modified: []
decisions:
  - id: ghost-mode-macos-target
    description: "Swift package targets macOS v13 (not macCatalyst) for NSPanel/AppKit access"
    rationale: "NSPanel is AppKit-only; Mac Catalyst doesn't expose AppKit directly"
  - id: click-through-hosting-view
    description: "ClickThroughHostingView overrides hitTest to pass clicks through transparent areas"
    rationale: "ignoresMouseEvents=true would block ALL interaction; selective hit-test preserves FAB/card taps"
  - id: dispatch-queue-main-not-mainactor
    description: "All UI operations use DispatchQueue.main.async, never @MainActor"
    rationale: "Lesson from Phase 03 -- @MainActor deadlocks when C# blocks with Task.Wait()"
  - id: universal-binary
    description: "xcframework is universal (arm64 + x86_64)"
    rationale: "Xcode builds both architectures by default; supports Intel and Apple Silicon Macs"
metrics:
  duration: "6 minutes"
  completed: "2026-02-26"
---

# Phase 04 Plan 01: GaimerGhostMode Native Swift Package Summary

**One-liner:** NSPanel-based transparent floating overlay with SwiftUI FAB and event cards, compiled as macOS xcframework with 13 @_cdecl exports for P/Invoke.

## What Was Built

Created the complete GaimerGhostMode Swift package -- a native macOS overlay system using NSPanel for a transparent, non-activating floating window. The package contains:

1. **GhostPanel** -- NSPanel subclass configured for overlay use (borderless, transparent, floating, non-activating, joins all Spaces, works alongside fullscreen apps)
2. **ClickThroughHostingView** -- NSHostingView subclass with selective hit-testing that passes clicks through transparent areas while intercepting taps on the FAB button and event cards
3. **GhostPanelContentView** -- SwiftUI root view composing FAB button at bottom-right with event cards stacked above
4. **FabButton** -- 56pt circular button with agent portrait, yellow glow ring when connected, dim state when disconnected, X icon when active
5. **EventCardView** -- Three card variants (Voice with animated dots, Text with title pill, TextWithImage) with 8-second auto-dismiss and tap-to-dismiss
6. **GaimerGhostMode.swift** -- 13 @_cdecl exported functions forming the C API surface for .NET P/Invoke consumption

## Architecture

```
C# DllImport (P/Invoke)
    |
    v
@_cdecl exports (GaimerGhostMode.swift)
    |
    v -- DispatchQueue.main.async --
    |
GhostPanel (NSPanel)
    |
    +-- ClickThroughHostingView (NSHostingView)
            |
            +-- GhostPanelContentView (SwiftUI)
                    |
                    +-- FabButton
                    +-- EventCardView (3 variants)
                    |
                    +-- GhostPanelState (ObservableObject)
```

## Exported Functions (13)

| Function | Purpose |
| --- | --- |
| ghost_panel_create | Create panel + state + SwiftUI content |
| ghost_panel_destroy | Close panel, nil all references |
| ghost_panel_show | orderFront -- bring panel to front |
| ghost_panel_hide | orderOut -- hide panel |
| ghost_panel_set_agent_image | Set FAB agent portrait path |
| ghost_panel_set_fab_active | Toggle FAB active/inactive state |
| ghost_panel_set_fab_connected | Toggle FAB connected glow |
| ghost_panel_show_card | Show event card (variant 1/2/3) |
| ghost_panel_dismiss_card | Dismiss current card |
| ghost_panel_set_fab_tap_callback | Register C function pointer for FAB tap |
| ghost_panel_set_card_dismiss_callback | Register C function pointer for card dismiss |
| ghost_panel_set_position | Set panel origin (x, y) |
| ghost_panel_set_size | Set panel content size |

## Decisions Made

| Decision | Rationale |
| --- | --- |
| Target macOS v13, not macCatalyst | NSPanel requires AppKit which is not available on Mac Catalyst |
| ClickThroughHostingView hitTest override | ignoresMouseEvents=true blocks ALL interaction; selective hit-test preserves FAB/card taps |
| DispatchQueue.main.async, never @MainActor | Phase 03 lesson: @MainActor deadlocks when C# blocks with Task.Wait() |
| Universal binary (arm64 + x86_64) | Supports both Intel and Apple Silicon Macs |
| ghost_panel_create uses DispatchQueue.main.sync | Called once at init from background thread; needs synchronous return for success bool |

## Deviations from Plan

### Minor Discrepancy

**1. [Plan Spec] Plan specifies 14 @_cdecl functions but only lists 13**

- **Found during:** Task 1 verification
- **Issue:** The plan frontmatter and verification section say "14 @_cdecl functions" but the detailed bullet list in the task action only enumerates 13 distinct functions
- **Resolution:** Implemented all 13 functions exactly as specified in the bullet list. The "14" appears to be a counting error in the plan document.
- **Impact:** None -- all specified functionality is present

## Verification Results

| Check | Result |
| --- | --- |
| swift package resolve | PASS |
| 6 Swift source files exist | PASS |
| Package.swift targets .macOS(.v13) | PASS |
| 13 @_cdecl exports (all specified functions) | PASS |
| No @MainActor annotations | PASS |
| DispatchQueue.main used (15 usages) | PASS |
| No ignoresMouseEvents in code | PASS |
| xcframework exists at destination | PASS |
| Mach-O universal binary (x86_64 + arm64) | PASS |
| nm shows 13 ghost_panel_* symbols | PASS |
| xcframework Info.plist shows macos platform | PASS |
| Build script is executable | PASS |

## Commits

| Hash | Description |
| --- | --- |
| 27a99ef | feat(04-01): create GaimerGhostMode Swift package with NSPanel and SwiftUI views |
| aa4e16b | feat(04-01): build GaimerGhostMode xcframework for macOS |

## Next Phase Readiness

Plan 04-02 can now proceed with:
- `GaimerGhostMode.xcframework` is at `Platforms/MacCatalyst/` ready for NativeReference
- All 13 @_cdecl function signatures are defined for DllImport declarations
- The C API follows the same pattern as GaimerScreenCapture (Phase 03)
