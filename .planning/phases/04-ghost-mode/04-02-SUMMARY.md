---
phase: 04
plan: 02
subsystem: ghost-mode-interop
tags: [pinvoke, dllimport, ghost-mode, native-interop, maui, maccatalyst]
dependency-graph:
  requires: [04-RESEARCH]
  provides: [IGhostModeService, GhostModeNativeMethods, MacGhostModeService, MockGhostModeService]
  affects: [04-03]
tech-stack:
  added: []
  patterns: [DllImport P/Invoke, GCHandle callback pinning, shared DllImportResolver, graceful degradation]
key-files:
  created:
    - src/GaimerDesktop/GaimerDesktop/Services/IGhostModeService.cs
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/GhostModeNativeMethods.cs
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/MacGhostModeService.cs
    - src/GaimerDesktop/GaimerDesktop/Services/MockGhostModeService.cs
  modified:
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/NativeMethods.cs
decisions:
  - id: shared-resolver
    description: Refactored DllImportResolver into shared ResolveFramework helper for both xcframeworks
    rationale: NativeLibrary.SetDllImportResolver only allows one registration per assembly
metrics:
  duration: ~5 minutes
  completed: 2026-02-27
---

# Phase 04 Plan 02: C# Interop Layer Summary

Complete C# interop layer for ghost mode using DllImport P/Invoke with GCHandle-pinned callbacks, same pattern as Phase 03 screen capture.

## What Was Built

### IGhostModeService (Interface)
Cross-platform service contract with 10 members:
- `IsGhostModeActive` / `IsSupported` properties
- `EnterGhostModeAsync()` / `ExitGhostModeAsync()` for mode transitions
- `SetAgentImage()` / `SetFabState()` for FAB configuration
- `ShowCard()` / `DismissCard()` for event cards
- `FabTapped` / `CardDismissed` events for native-to-managed callbacks

### GhostModeNativeMethods (DllImport Declarations)
14 P/Invoke declarations matching the @_cdecl exports from the GaimerGhostMode xcframework:
- Panel lifecycle: `ghost_panel_create`, `ghost_panel_destroy`
- Visibility: `ghost_panel_show`, `ghost_panel_hide`
- FAB config: `ghost_panel_set_agent_image`, `ghost_panel_set_fab_active`, `ghost_panel_set_fab_connected`
- Event cards: `ghost_panel_show_card`, `ghost_panel_dismiss_card`
- Callbacks: `ghost_panel_set_fab_tap_callback`, `ghost_panel_set_card_dismiss_callback`
- Layout: `ghost_panel_set_position`, `ghost_panel_set_size`

All with correct marshaling: `LPUTF8Str` for strings, `U1` for bools, `Cdecl` calling convention.

### MacGhostModeService (macOS Implementation)
- Constructor calls `ghost_panel_create()`, stores success in `_isSupported`
- GCHandle pins FAB tap and card dismiss callbacks to prevent GC collection
- `EnterGhostModeAsync`: hides UIWindow, shows native panel
- `ExitGhostModeAsync`: hides native panel, restores UIWindow
- All methods are no-ops when `!_isSupported` (graceful degradation)
- IDisposable: frees GCHandle, destroys native panel

### NativeMethods.cs (Extended DllImportResolver)
Refactored from single-library resolver to shared `ResolveFramework(string name)` helper:
- `GaimerScreenCapture` -> resolves SCK framework (existing)
- `GaimerGhostMode` -> resolves ghost mode framework (new)
- Both try flat path then versioned (Versions/A/) path
- Single `SetDllImportResolver` registration serves entire assembly

### MockGhostModeService (No-op Fallback)
- `IsGhostModeActive` / `IsSupported` always false
- All methods return immediately (Task.CompletedTask for async)
- Console.WriteLine on every call for debug tracing
- Events declared but never fired

## Decisions Made

| Decision | Rationale |
| --- | --- |
| Shared `ResolveFramework` helper instead of separate resolvers | `NativeLibrary.SetDllImportResolver` only allows one registration per assembly; dispatch on library name |
| Named method delegates instead of inline lambdas for callbacks | Cleaner for GCHandle pinning, more readable |
| Try/catch around ghost_panel_create for DllNotFoundException | Framework may not be bundled yet (Plan 01 handles that) |

## Deviations from Plan

None -- plan executed exactly as written.

## Verification Results

| Check | Result |
| --- | --- |
| Build (0 errors) | PASS |
| IGhostModeService members >= 10 | 14 (PASS) |
| DllImport count = 14 | 14 (PASS) |
| NativeMethods handles both libraries | PASS |
| MacGhostModeService implements IDisposable | PASS |
| GCHandle usage in MacGhostModeService | PASS |
| MockGhostModeService implements IGhostModeService | PASS |

## Commits

| Hash | Description |
| --- | --- |
| 205675b | feat(04-02): add IGhostModeService interface and DllImport declarations |
| 3a94a05 | feat(04-02): add MacGhostModeService, extend DllImportResolver, add mock |

## Next Phase Readiness

Plan 04-02 provides the complete C# interop layer. Plan 04-03 (DI registration and integration wiring) can proceed -- it will register IGhostModeService in MauiProgram.cs and wire ghost mode toggling into MainViewModel.

The actual GaimerGhostMode.xcframework does not need to exist for compilation. Plan 04-01 (native Swift xcframework) can be built independently and the framework resolved at runtime.
