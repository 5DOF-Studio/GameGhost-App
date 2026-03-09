---
phase: 03-screen-capture
plan: 02
subsystem: capture
tags: [pinvoke, screencapturekit, interop, maccatalyst, gchandle, callback, fallback]

# Dependency graph
requires:
  - phase: 03-screen-capture
    plan: 01
    provides: "GaimerScreenCapture.xcframework with sck_is_available and sck_capture_window @_cdecl exports"
provides:
  - "P/Invoke declarations for SCK native helper (NativeMethods.sck_is_available, sck_capture_window)"
  - "WindowCaptureService SCK integration with graceful fallback to CGDisplayCreateImage"
  - "GCHandle-pinned callback pattern for async native-to-managed interop"
affects: [03-03-screen-capture-integration]

# Tech tracking
tech-stack:
  added: []
  patterns: ["GCHandle.Alloc for pinning managed delegates during native async calls", "TaskCompletionSource bridge between native callbacks and C# async", "Static availability cache with DllNotFoundException guard"]

key-files:
  created: []
  modified:
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/NativeMethods.cs
    - src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/WindowCaptureService.cs

key-decisions:
  - "GCHandle.Alloc to pin callback delegate -- prevents GC during async native call"
  - "TaskCompletionSource<byte[]> to bridge native callback to synchronous Wait -- timer tick is already background thread"
  - "Per-frame fallback -- if SCK fails for one frame, CGDisplayCreateImage used for that frame only"
  - "10-second timeout on SCK capture -- prevents indefinite hang if native call never completes"
  - "2x resolution multiplier for Retina displays -- SCK captures at pixel resolution"

patterns-established:
  - "Native callback pattern: delegate + GCHandle.Alloc + try/finally handle.Free"
  - "Availability caching: static readonly bool with try/catch for DllNotFoundException"
  - "SCK-first with per-frame fallback to CGDisplayCreateImage"

# Metrics
duration: 3min
completed: 2026-02-25
---

# Phase 03 Plan 02: Screen Capture Service Summary

**P/Invoke declarations and WindowCaptureService SCK integration with GCHandle-pinned callback and per-frame CGDisplayCreateImage fallback**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-26T00:21:51Z
- **Completed:** 2026-02-26T00:24:26Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Added SckCaptureCallback delegate type and P/Invoke declarations for sck_is_available and sck_capture_window to NativeMethods.cs
- Integrated SCK capture into WindowCaptureService with CaptureWithScreenCaptureKit method using TaskCompletionSource and 10-second timeout
- SCK used on macOS 14+ with per-frame fallback to CGDisplayCreateImage if SCK fails
- Callback delegate pinned via GCHandle to prevent GC during native async call
- Existing interface contract (IWindowCaptureService), window enumeration, timer, and thumbnail generation all preserved unchanged

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SCK P/Invoke declarations to NativeMethods.cs** - `f972cfc` (feat)
2. **Task 2: Integrate SCK capture into WindowCaptureService with fallback** - `84d6ecc` (feat)

## Files Created/Modified
- `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/NativeMethods.cs` - Added SckCaptureCallback delegate, sck_is_available and sck_capture_window P/Invoke declarations targeting GaimerScreenCapture xcframework
- `src/GaimerDesktop/GaimerDesktop/Platforms/MacCatalyst/WindowCaptureService.cs` - Added CheckSckAvailability, CaptureWithScreenCaptureKit methods; modified OnCaptureTimerTick for SCK-first with fallback; added startup capture method logging

## Decisions Made
- **GCHandle.Alloc for delegate pinning:** The native callback delegate must survive GC during the async SCK call. GCHandle.Alloc keeps the managed delegate alive until handle.Free in the finally block.
- **TaskCompletionSource bridge:** The native callback fires asynchronously, but OnCaptureTimerTick runs on a timer thread. TCS with .Wait(10s) bridges the two patterns cleanly.
- **Per-frame fallback:** If SCK fails for a single frame, we fall back to CGDisplayCreateImage for that frame only rather than permanently disabling SCK. Transient failures (e.g. window minimized) should not disable the better capture path.
- **2x Retina multiplier:** SCK captures at pixel resolution, so we request 2x the logical window dimensions to get Retina-quality captures matching the display density.
- **Static availability cache:** sck_is_available result cached at class load since macOS version cannot change at runtime. DllNotFoundException caught defensively in case xcframework fails to load.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - both tasks compiled and passed verification on first attempt.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- SCK P/Invoke and WindowCaptureService integration complete
- Next plan (03-03) should wire capture into the brain pipeline: BrainContextService.OnScreenCapture, timer management in SessionManager, and MainViewModel integration
- The FrameCaptured event fires with byte[] PNG data from either SCK or CGDisplayCreateImage -- downstream code is capture-method-agnostic
- Screen Recording permission (existing CGPreflightScreenCaptureAccess) also covers SCK -- no new entitlements needed

---
*Phase: 03-screen-capture*
*Completed: 2026-02-25*
