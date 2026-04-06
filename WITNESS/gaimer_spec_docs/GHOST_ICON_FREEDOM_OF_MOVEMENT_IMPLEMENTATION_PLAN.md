# Ghost Icon Freedom Of Movement Implementation Plan

**Status:** Research complete, implementation deferred to dedicated session  
**Created:** 2026-03-14  
**Scope:** macOS ghost-mode FAB/panel movement freedom for the native ghost overlay

## Summary

The ghost icon should become freely movable by the user during ghost mode, with stable drag behavior, screen-safe clamping, and remembered placement. This should be implemented in the existing native `GaimerGhostMode` AppKit layer, not in .NET MAUI UI code.

The current codebase already has the right seam:
- C# side: `MacGhostModeService.SetPosition(...)`
- native bridge: `ghost_panel_set_position(double x, double y)`
- native host: `GaimerGhostMode` xcframework

What is missing is drag gesture handling and position persistence in the native panel itself.

## Recommendation

Use a native AppKit drag implementation inside the ghost panel:

1. Track drag gestures in the native ghost panel/FAB host view.
2. Move the panel by updating its window frame origin in screen coordinates.
3. Clamp movement to the current screen's visible bounds.
4. Persist the last stable position.
5. Restore that position on next ghost entry, with fallback if the screen layout changed.

This should remain a Swift/AppKit responsibility. The .NET layer should only:
- receive persisted/restored positions if needed
- optionally request reset-to-default
- stay out of real-time drag math

## Why This Direction

### Best fit for the current architecture

The project already uses a native AppKit ghost panel through `GaimerGhostMode`. Adding drag in .NET MAUI would fight the actual window owner and create coordinate-sync problems.

### Most stable behavior on macOS

AppKit owns:
- panel/window movement
- screen coordinates
- multi-display placement
- visible-frame clamping
- mouse/gesture event timing

That makes native drag handling the lowest-risk path.

### Keeps Catalyst complexity contained

Mac Catalyst itself does not give a clean managed `NSPanel`-level movement abstraction in the shared MAUI layer. The existing native helper is already the correct escape hatch.

## Research Findings

### 1. Native AppKit should own movement

Apple’s AppKit model is window-centric, and the relevant primitives are on `NSWindow`/`NSScreen` rather than MAUI controls.

Useful primitives:
- `NSWindow.isMovableByWindowBackground`
- `NSWindow.setFrameOrigin(_:)`
- `NSScreen.visibleFrame`

Practical implication:
- use AppKit window movement and clamping in the native panel
- do not try to simulate floating movement purely with MAUI layout offsets

### 2. `isMovableByWindowBackground` is not enough on its own

This looks attractive, but it is risky for this product because the ghost panel contains interactive controls:
- FAB tap
- card dismiss
- gear tap
- audio toggles

Blindly making the whole background movable can interfere with click/drag discrimination and interactive child controls.

Recommended interpretation:
- use explicit drag handling on a dedicated draggable region, or custom drag logic in the host view
- do not rely on a blanket “entire window background is draggable” approach as the primary solution

### 3. Position must be clamped to `visibleFrame`

Apple documents `NSScreen.visibleFrame` as the safe visible screen area excluding menu bar, dock, and current UI exclusions. This is the correct clamp target for a floating ghost panel.

Implication:
- persist a panel anchor or origin
- clamp restored and dragged positions against current `visibleFrame`
- never assume full screen frame is safe

### 4. Multi-monitor resilience matters

Free movement without restore logic becomes fragile when:
- displays are disconnected
- display arrangement changes
- resolution/scaling changes
- current target screen changes

The restored position must degrade safely:
- if prior screen is gone, move to current main/target screen
- if prior point is off-screen, clamp into `visibleFrame`

### 5. Snap behavior is optional and should be phase 2

There are proven floating-button libraries that use drag plus snap-to-edge/corner successfully, but your stated goal is freedom of movement first.

So phase ordering should be:
- Phase 1: free drag + clamp + persist
- Phase 2: optional snap or magnetic settling
- Phase 3: gestures/polish/accessibility

## External References

### Apple / platform docs

- Apple: [`isMovableByWindowBackground`](https://developer.apple.com/documentation/appkit/nswindow/ismovablebywindowbackground)
- Apple: [`setFrameOrigin(_:)`](https://developer.apple.com/documentation/appkit/nswindow/setframeorigin%28_%3A%29)
- Apple: [`NSScreen.visibleFrame`](https://developer.apple.com/documentation/AppKit/NSScreen/visibleFrame)
- Apple archive: [High Resolution APIs and coordinate/backing guidance](https://developer.apple.com/library/archive/documentation/GraphicsAnimation/Conceptual/HighResolutionOSX/APIs/APIs.html)

### Swift/AppKit adjacent repos and examples

- [`sarunw/FloatingPanel.swift` gist](https://gist.github.com/sarunw/26725860e3ac318971b7bc84a54d14b7)
  - useful as a minimal `NSPanel` configuration example
  - relevant for panel style/behavior, not drag logic by itself
- [`Hover`](https://github.com/pedrommcarrasco/Hover) via CocoaPods listing: [Hover pod page](https://cocoapods.org/pods/Hover)
  - relevant for drag + snap UX ideas
  - iOS-oriented, so adapt behavior ideas, not implementation directly
- [`GlobalFloatingButton`](https://cocoapods.org/pods/GlobalFloatingButton)
  - relevant for safe-area-aware drag and snap concepts
  - again iOS-focused, not a drop-in macOS solution

### .NET / interop references

- [`dotnet/macios`](https://github.com/dotnet/macios)
  - confirms the Apple SDK binding surface and platform model on .NET
  - useful when validating whether AppKit-level control belongs in managed code or native interop
- [`bielikb/xcframeworks`](https://github.com/bielikb/xcframeworks)
  - useful reference for xcframework packaging/integration patterns
- [`unsignedapps/swift-create-xcframework`](https://github.com/unsignedapps/swift-create-xcframework)
  - useful if the native ghost framework needs restructured build/distribution automation later

## Recommended Implementation Shape

### Native Swift/AppKit

Add drag support inside the native ghost panel implementation:

- Add mouse/pan handling in the ghost panel host view or FAB hit region.
- Capture:
  - initial mouse-down point in screen coordinates
  - initial panel origin
- On drag:
  - compute delta in screen coordinates
  - propose new panel origin
  - clamp against the owning screen’s `visibleFrame`
  - call `setFrameOrigin`
- On drag end:
  - persist the final stable origin and screen identity

### C# / bridge

Keep managed responsibilities thin:

- optional API to request:
  - reset to default position
  - query current position
  - maybe enable/disable free movement in settings later
- no real-time drag loops in C#

## Workarounds Considered

### A. Entire window draggable via `isMovableByWindowBackground`

Pros:
- small implementation surface

Cons:
- risky with interactive controls embedded in the panel
- poor control over drag zones
- likely to create accidental movement during clicks/taps

Decision:
- not preferred as the primary solution

### B. MAUI-side drag gesture moving a mirrored overlay state

Pros:
- keeps more logic in C#

Cons:
- wrong owner: native AppKit panel actually moves on screen
- coordinate conversion complexity
- likely drift and race conditions
- poor multi-display behavior

Decision:
- reject

### C. Snap-only movement

Pros:
- simpler mental model

Cons:
- does not satisfy “freedom of movement”
- constrains the UX before validating the unrestricted version

Decision:
- defer to phase 2 if still desired

## Phased Execution Plan

### Phase 1: Native Free Drag MVP

Owner:
- Codex/native-interoperability lane

Deliverables:
- draggable ghost FAB/panel in native AppKit layer
- clamped movement to current `visibleFrame`
- persisted last position
- safe restore on next entry

Acceptance:
- user can drag the ghost icon freely
- icon never restores off-screen
- icon remains reachable with dock/menu bar/display changes
- no regression to FAB tap, gear tap, audio toggles, or card dismiss

### Phase 2: Restore/Screen Topology Hardening

Owner:
- Codex/native-interoperability lane

Deliverables:
- screen identity persistence
- fallback when saved display is unavailable
- clamp on resolution/scaling changes

Acceptance:
- unplugging/rearranging displays does not orphan the ghost icon

### Phase 3: UX Polish

Owner:
- shared

Options:
- optional edge magnetism / soft snap
- reset position command
- drag affordance hint
- reduced-motion polish

Acceptance:
- movement feels intentional, not twitchy
- placement does not block core game HUDs more than necessary

## Test Plan

### Functional

- drag on single display
- drag across all corners/edges
- enter/exit ghost mode retains last position
- card interactions still work after drag
- FAB tap still toggles correctly

### Multi-display

- restore after disconnecting secondary monitor
- restore after changing main display
- restore after resolution/scaling change

### Usability

- user can still intentionally tap without accidental drag
- drag threshold prevents jitter
- placement never hides under menu bar/dock

### Failure / fallback

- invalid persisted position falls back to default anchor
- unavailable screen falls back to current active screen

## Risks

### Input conflict risk

Drag recognition can steal taps from:
- FAB toggle
- card dismiss
- audio toggles

Mitigation:
- use a movement threshold before entering drag mode
- prefer a defined draggable region if whole-panel dragging proves noisy

### Coordinate-system risk

AppKit screen coordinates, backing scale, and Catalyst/native boundaries can drift if conversion is done casually.

Mitigation:
- stay in native AppKit screen coordinates for movement math
- use `visibleFrame`
- avoid mixing MAUI layout coordinates into drag calculations

### Restore risk

Saved positions can become invalid when screens change.

Mitigation:
- persist screen identifier plus origin
- clamp and fallback on every restore

## Recommended Next Session

1. Inspect the Swift source for `GaimerGhostMode` and identify the actual FAB host view and panel class.
2. Implement native drag tracking with a small drag threshold.
3. Add clamped `setFrameOrigin` movement against `visibleFrame`.
4. Add persistence and fallback restore.
5. Validate single-display and multi-display behavior manually.

## PM View

### Decision

This feature should proceed as a dedicated native/AppKit improvement, not as a MAUI interaction project.

### Owner split

- Codex: native/AppKit research and implementation
- Claude: only optional later polish if a MAUI-side affordance or settings surface is needed

### Gate

Do not mix this feature into the current archived-boundary/timeline slice. It deserves its own session because it is native, interaction-heavy, and requires manual testing across screen configurations.
