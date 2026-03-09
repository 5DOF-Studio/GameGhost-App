---
phase: 06-capture-pipeline
plan: 03
subsystem: pipeline
tags: [brain-voice-pipeline, frame-diff, dHash, DI, context-pipeline, L1-events]

# Dependency graph
requires:
  - phase: 06-01
    provides: IFrameDiffService with dHash perceptual hashing
  - phase: 06-02
    provides: IBrainContextService with L1 event store and IngestEventAsync
  - phase: 05-04
    provides: IBrainService, BrainEventRouter channel consumer, OpenRouterBrainService
provides:
  - Corrected capture pipeline enforcing brain-voice golden rule
  - Frame diff gate preventing redundant brain API calls
  - L1 event ingestion connecting brain output to context memory
  - Complete DI wiring for all Phase 06 services
affects: [07-persistence, 08-polish]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Brain-only capture pipeline (voice never receives raw images)"
    - "Diff precept gating (dHash HasChanged before brain submission)"
    - "L1 event creation from BrainResult routing"
    - "Optional DI dependency pattern (IBrainContextService? with default null)"

key-files:
  created: []
  modified:
    - src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs
    - src/GaimerDesktop/GaimerDesktop/Services/BrainEventRouter.cs
    - src/GaimerDesktop/GaimerDesktop/MauiProgram.cs

key-decisions:
  - "Removed SendImageAsync from FrameCaptured handler entirely (Golden Rule enforcement)"
  - "HasChanged diff gate before brain submission eliminates redundant API calls on unchanged frames"
  - "BrainEvent type mapping: ImageAnalysis->VisionObservation, ProactiveAlert/ToolResult->GameplayState, Error->SystemSignal"
  - "Error BrainResults ingested with Confidence 0.0 (filtered by context service threshold automatically)"
  - "ImageAnalysis events get 30s ValidFor, others get 2min"
  - "IBrainContextService as optional parameter with null default for backward compatibility"

patterns-established:
  - "Brain-only visual pipeline: capture -> diff gate -> brain submit -> router -> text to voice/timeline"
  - "Optional DI injection via nullable constructor parameter with default null"
  - "Fire-and-forget IngestEventAsync with ContinueWith fault logging"

# Metrics
duration: 12min
completed: 2026-03-02
---

# Phase 06 Plan 03: Pipeline Enforcement Summary

**Capture pipeline rewritten to enforce brain-voice golden rule: removed SendImageAsync, added dHash diff gate, wired L1 event ingestion from BrainEventRouter to IBrainContextService**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-02T21:04:59Z
- **Completed:** 2026-03-02T21:17:10Z
- **Tasks:** 3/3
- **Files modified:** 3

## Accomplishments
- Removed the legacy SendImageAsync call from the capture pipeline, enforcing the core IP rule that voice NEVER receives raw image bytes
- Added IFrameDiffService.HasChanged() diff precept gate so brain only processes materially changed frames (dHash Hamming distance > 10)
- Wired BrainEventRouter to create BrainEvent from each routed BrainResult and ingest into IBrainContextService L1 store
- Registered IFrameDiffService in DI and updated BrainEventRouter factory to include IBrainContextService

## Task Commits

Each task was committed atomically:

1. **Task 1: Rewrite MainViewModel capture pipeline** - `4168141` (feat)
2. **Task 2: Wire BrainEventRouter L1 event ingestion** - `dbbe640` (feat)
3. **Task 3: Register IFrameDiffService in DI** - `430641c` (feat)

## Files Modified
- `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs` - Removed SendImageAsync, added IFrameDiffService injection and HasChanged diff gate in FrameCaptured handler
- `src/GaimerDesktop/GaimerDesktop/Services/BrainEventRouter.cs` - Added IBrainContextService optional dependency, L1 event creation in RouteBrainResult with type/category mapping
- `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs` - Registered IFrameDiffService singleton, updated BrainEventRouter factory to resolve and pass IBrainContextService

## Decisions Made
- **SendImageAsync removal is unconditional** -- not gated behind a feature flag. The old path violated the core pipeline architecture and must not exist.
- **BrainEvent type mapping** follows the spec: ImageAnalysis maps to VisionObservation, ProactiveAlert and ToolResult map to GameplayState, Error maps to SystemSignal.
- **Error results are still ingested** (not filtered) but with Confidence 0.0, which means the context service's confidence threshold (>= 0.3) naturally excludes them from context envelopes.
- **ValidFor differentiation**: ImageAnalysis observations expire in 30s (next capture supersedes), while alerts/tool results persist for 2 minutes.
- **IBrainContextService is nullable** in BrainEventRouter constructor for backward compatibility with existing tests/mocks that don't provide it.

## Deviations from Plan

None -- plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None -- no external service configuration required.

## Next Phase Readiness
- Phase 06 (Capture Pipeline) is now complete. All 3 plans executed successfully.
- The brain-voice pipeline golden rule is fully enforced in code.
- Frame diff gating eliminates redundant brain API calls on unchanged frames.
- L1 context pipeline is connected: brain results flow into the context service for voice/chat consumers.
- Ready for Phase 07 (Persistence Layer) -- SQLite + EF Core for chat history and session replay.

---
*Phase: 06-capture-pipeline*
*Completed: 2026-03-02*
