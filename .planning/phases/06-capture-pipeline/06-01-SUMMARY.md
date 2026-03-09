---
phase: 06-capture-pipeline
plan: 01
subsystem: capture
tags: [dhash, perceptual-hashing, skiasharp, frame-diff, image-processing]

# Dependency graph
requires:
  - phase: 03-screen-capture
    provides: WindowCaptureService provides raw frame bytes (PNG) for diff detection
  - phase: 05-brain-infrastructure
    provides: IBrainService pipeline receives frames gated by diff detection
provides:
  - IFrameDiffService interface with ComputeHash, CompareHashes, HasChanged
  - FrameDiffService implementation using dHash algorithm via SkiaSharp
  - CropRect record for region-of-interest hashing
  - FrameChangeEventArgs for debounced change notifications
affects: [06-02 (capture pipeline wiring), 06-03 (DI registration)]

# Tech tracking
tech-stack:
  added: [] # SkiaSharp already bundled with MAUI
  patterns: [dHash perceptual hashing, Hamming distance comparison, debounced state tracking]

key-files:
  created:
    - src/GaimerDesktop/GaimerDesktop/Services/IFrameDiffService.cs
    - src/GaimerDesktop/GaimerDesktop/Services/FrameDiffService.cs
  modified:
    - src/GaimerDesktop/GaimerDesktop/Services/BrainContextService.cs

key-decisions:
  - "dHash over pHash/SSIM -- <0.5ms per frame, perfect for board-state change detection"
  - "Default threshold 10 -- balances sensitivity (piece moves) vs noise tolerance (cursor, highlights)"
  - "1.5s debounce window -- prevents rapid-fire change events during animations"
  - "SkiaSharp for decode/resize -- zero new dependencies (MAUI built-in)"

patterns-established:
  - "dHash pattern: 9x8 Gray8 resize, horizontal pixel comparison, 64-bit hash"
  - "CropRect ROI pattern: extract subset before hashing for board-focused detection"
  - "Stateful HasChanged with debounce: internal _lastHash + _lastChangeTime tracking"

# Metrics
duration: 12min
completed: 2026-03-02
---

# Phase 6 Plan 1: IFrameDiffService Summary

**dHash perceptual hashing service (64-bit, <0.5ms/frame) with SkiaSharp decode, Hamming distance comparison, and 1.5s debounced change detection**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-02T20:48:31Z
- **Completed:** 2026-03-02T21:00:38Z
- **Tasks:** 2
- **Files created:** 2
- **Files modified:** 1

## Accomplishments
- IFrameDiffService interface with full dHash API contract (ComputeHash, CompareHashes, HasChanged, FrameChanged event)
- FrameDiffService implementation using dHash algorithm: 9x8 Gray8 resize via SkiaSharp, 64-bit perceptual hash, BitOperations.PopCount for Hamming distance
- ROI support via CropRect for board-region focused detection
- Stateful HasChanged with configurable threshold (default=10) and 1.5s debounce window
- Zero new NuGet dependencies (SkiaSharp is MAUI built-in)

## Task Commits

Each task was committed atomically:

1. **Task 1: Create IFrameDiffService interface** - `3942b5b` (feat)
2. **Task 2: Implement FrameDiffService with dHash algorithm** - `5620d96` (feat)

## Files Created/Modified
- `src/GaimerDesktop/GaimerDesktop/Services/IFrameDiffService.cs` - Interface contract with ComputeHash, CompareHashes, HasChanged; CropRect record; FrameChangeEventArgs class
- `src/GaimerDesktop/GaimerDesktop/Services/FrameDiffService.cs` - dHash implementation using SkiaSharp decode/resize, BitOperations.PopCount, debounced state tracking
- `src/GaimerDesktop/GaimerDesktop/Services/BrainContextService.cs` - Added IngestEventAsync implementation (L1 event store with retention window, L2 rolling summary)

## Decisions Made
- Used dHash over pHash/SSIM for speed (<0.5ms vs 2-5ms) -- board-state detection doesn't need photo-quality perception
- Default Hamming distance threshold of 10 per research doc -- balances piece-move sensitivity vs noise tolerance
- 1.5-second debounce window prevents rapid-fire events during game animations or transitions
- SkiaSharp Gray8 resize for minimal memory footprint (72 bytes working set for 9x8 image)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] BrainContextService missing IngestEventAsync implementation**
- **Found during:** Task 1 (build verification)
- **Issue:** BrainContextService did not implement IBrainContextService.IngestEventAsync, causing CS0535 compilation error
- **Fix:** Added IngestEventAsync with L1 event store (lock-protected list, 5-min retention window, 200-event cap, priority categories)
- **Files modified:** src/GaimerDesktop/GaimerDesktop/Services/BrainContextService.cs
- **Verification:** Build succeeded with 0 errors after fix
- **Committed in:** 3942b5b (Task 1 commit)

**2. [Rule 1 - Bug] Added null check on ExtractSubset return value**
- **Found during:** Task 2 (ROI ComputeHash implementation)
- **Issue:** Research doc code did not check ExtractSubset return value; could produce invalid bitmap on out-of-bounds ROI
- **Fix:** Added `if (!bitmap.ExtractSubset(cropped, subset)) return 0;` guard
- **Files modified:** src/GaimerDesktop/GaimerDesktop/Services/FrameDiffService.cs
- **Verification:** Build succeeded, defensive return prevents crash on invalid ROI
- **Committed in:** 5620d96 (Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 blocking, 1 bug)
**Impact on plan:** Both fixes necessary for correctness. No scope creep.

## Issues Encountered
- Transient CopyRefAssembly file lock error on first Task 2 build -- resolved on immediate retry (known .NET SDK race condition)

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- IFrameDiffService and FrameDiffService ready for DI registration in 06-03
- Capture pipeline wiring (06-02) can integrate HasChanged into frame capture flow
- No blockers for next plans

---
*Phase: 06-capture-pipeline*
*Completed: 2026-03-02*
