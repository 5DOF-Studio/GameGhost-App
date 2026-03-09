---
phase: 08-polish
plan: 04
subsystem: capture-pipeline
tags: [capture, configuration, parameterization, agent-config]
dependency-graph:
  requires: [08-01, 08-03]
  provides: [CaptureConfig record, parameterized capture services, per-agent pipeline config]
  affects: [future FPS/RPG agent onboarding]
tech-stack:
  added: []
  patterns: [record-based config, constructor injection with defaults, per-call threshold override]
key-files:
  created: []
  modified:
    - src/GaimerDesktop/GaimerDesktop/Models/Agent.cs
    - src/GaimerDesktop/GaimerDesktop/Services/FrameDiffService.cs
    - src/GaimerDesktop/GaimerDesktop/Services/IFrameDiffService.cs
    - src/GaimerDesktop/GaimerDesktop/Services/IWindowCaptureService.cs
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/WindowCaptureService.cs
    - src/GaimerDesktop/GaimerDesktop/Platforms/Windows/WindowCaptureService.cs
    - src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs
    - src/GaimerDesktop/GaimerDesktop.Tests/FrameAnalysis/FrameDiffServiceTests.cs
decisions:
  - id: capture-config-record-defaults
    summary: CaptureConfig defaults match chess values (30s/10/1.5s) for zero behavioral change
  - id: threshold-per-call-override
    summary: DiffThreshold passed per-call to HasChanged rather than recreating FrameDiffService per session
  - id: debounce-kept-at-di-level
    summary: Debounce window stays at DI singleton level; per-agent debounce deferred to future enhancement
metrics:
  duration: 8m
  completed: 2026-03-05
  tests-added: 3
  tests-total: 502
---

# Phase 08 Plan 04: Capture Pipeline Parameterization Summary

**One-liner:** CaptureConfig record on Agent model with per-agent interval/threshold/debounce, wired through MainViewModel to FrameDiffService and WindowCaptureService.

## What Was Done

### Task 1: Add CaptureConfig record + parameterize services (already committed in prior session)
- Added `CaptureConfig` record with `CaptureIntervalMs`, `DiffThreshold`, `DebounceWindowSeconds`, `AutoCapture` properties
- Added `CaptureConfig` property to `Agent` class with chess-tuned defaults (`new()`)
- Set RPG-tuned config on Derek agent (threshold=8, debounce=1.0s)
- Parameterized `FrameDiffService` constructor with `debounceWindow` and `defaultThreshold` params
- Changed `HasChanged` default threshold from hardcoded 10 to -1 sentinel that falls back to constructor default
- Added `captureIntervalMs` parameter to `IWindowCaptureService.StartCaptureAsync`
- Updated both MacCatalyst and Windows `WindowCaptureService` to accept and use the interval parameter

### Task 2: Wire CaptureConfig into MainViewModel pipeline + update tests
- `SelectTargetAsync` now reads `SelectedAgent.CaptureConfig.CaptureIntervalMs` and passes to `StartCaptureAsync`
- `FrameCaptured` handler reads `SelectedAgent.CaptureConfig.DiffThreshold` and passes to `HasChanged`
- Added 3 new FrameDiffService tests:
  - `Constructor_CustomDebounce_UsesProvidedValue` -- 200ms debounce validates faster rate
  - `Constructor_CustomThreshold_UsesProvidedDefault` -- high default threshold controls triggering
  - `HasChanged_ExplicitThreshold_OverridesDefault` -- per-call threshold overrides constructor default

## Decisions Made

| Decision | Rationale |
| --- | --- |
| CaptureConfig record defaults = chess values | Zero behavioral change for existing agents; new agents override |
| Threshold passed per-call, not per-DI-instance | FrameDiffService is DI singleton; threshold varies per agent selection |
| Debounce stays at DI level for now | Recreating FrameDiffService per-session would require DI scope changes; deferred |
| Derek gets threshold=8, debounce=1.0s | RPG scenes change more subtly; lower threshold catches narrative beats |

## Deviations from Plan

**Task 1 was already committed in a prior session** (commit `52cc1ec`). The CaptureConfig record, service parameterization, and Agent property were all part of that commit. This execution verified the changes were correct and added the missing MainViewModel wiring (Task 2) which was not yet done.

## Test Results
- Before: 499 passing
- After: 502 passing (+3 new constructor parameterization tests)
- No regressions

## Next Phase Readiness
- Pipeline is ready for FPS agents (fast capture: 1-5s interval, threshold=5, debounce=0.3s)
- Pipeline is ready for RPG agents (Derek already configured)
- Future enhancement: per-session FrameDiffService recreation for agent-specific debounce windows
