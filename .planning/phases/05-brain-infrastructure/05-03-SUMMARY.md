---
phase: 05-brain-infrastructure
plan: 03
subsystem: brain-pipeline
tags: [channel-reader, brain-event-router, async-consumer, main-thread-marshaling, voice-forwarding]

# Dependency graph
requires:
  - phase: 02-chat-brain
    provides: BrainEventRouter, IBrainEventRouter, ITimelineFeed, IConversationProvider
  - phase: 05-01
    provides: BrainResult, BrainResultType, BrainResultPriority enums
provides:
  - BrainEventRouter Channel consumer loop (StartConsuming/StopConsuming)
  - RouteBrainResult mapping from BrainResultType to existing timeline/voice methods
  - Background-to-MainThread marshaling for UI operations
affects:
  - 05-04 (DI wiring calls StartConsuming with IBrainService.Results)
  - 06-polish (error handling, testing)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "ChannelReader<BrainResult> async consumption with ReadAllAsync in background Task"
    - "MainThread.BeginInvokeOnMainThread for UI marshaling from consumer thread"
    - "CancellationTokenSource.CreateLinkedTokenSource for composable cancellation"
    - "Per-result try/catch to prevent single bad result from killing consumer loop"

key-files:
  created: []
  modified:
    - src/GaimerDesktop/GaimerDesktop/Services/IBrainEventRouter.cs
    - src/GaimerDesktop/GaimerDesktop/Services/BrainEventRouter.cs

key-decisions:
  - "Additive change only -- all 6 existing direct-call methods untouched"
  - "RouteBrainResult dispatches to existing OnImageAnalysis/OnProactiveAlert/OnGeneralChat methods"
  - "Error type logged to Debug and surfaced via top strip (not timeline)"
  - "Voice forwarding: Interrupt gets [URGENT] prefix, WhenIdle is normal, Silent is skipped"
  - "StopConsuming is fire-and-forget (no await) with internal error handling in loop"

patterns-established:
  - "Channel consumer coexists with existing sync methods -- dual ingestion paths"
  - "Linked CTS pattern for composable cancellation (session CTS + internal CTS)"

# Metrics
duration: 4min
completed: 2026-03-02
---

# Phase 05 Plan 03: BrainEventRouter Channel Consumer Summary

**Async Channel consumer loop bridging IBrainService results to timeline/voice/UI via RouteBrainResult with MainThread marshaling**

## Performance

- **Duration:** 4 min
- **Started:** 2026-03-02T17:46:22Z
- **Completed:** 2026-03-02T17:50:13Z
- **Tasks:** 1
- **Files modified:** 2

## Accomplishments
- Added StartConsuming and StopConsuming to IBrainEventRouter interface (8 methods total: 6 original + 2 new)
- Implemented ConsumeBrainResultsAsync background loop consuming ChannelReader<BrainResult> with ReadAllAsync
- RouteBrainResult maps all 4 BrainResultType values to existing timeline/voice methods with MainThread.BeginInvokeOnMainThread marshaling
- Voice forwarding respects BrainResultPriority: Interrupt gets [URGENT] prefix, WhenIdle sends normally, Silent skips voice entirely
- Per-result error handling ensures one bad result cannot kill the consumer loop
- Linked CancellationTokenSource enables clean shutdown via StopConsuming or external cancellation

## Task Commits

Each task was committed atomically:

1. **Task 1: Add Channel consumer to IBrainEventRouter and BrainEventRouter** - `75e436c` (feat)

## Files Modified
- `Services/IBrainEventRouter.cs` - Added StartConsuming(ChannelReader<BrainResult>, CancellationToken) and StopConsuming() to interface
- `Services/BrainEventRouter.cs` - Added Channel Consumer region with StartConsuming, StopConsuming, ConsumeBrainResultsAsync, RouteBrainResult

## Decisions Made
- **Additive change only:** All 6 existing direct-call methods (OnScreenCapture, OnBrainHint, OnImageAnalysis, OnDirectMessage, OnProactiveAlert, OnGeneralChat) remain completely untouched. The Channel consumer is a second ingestion path.
- **RouteBrainResult reuses existing methods:** ImageAnalysis -> OnImageAnalysis, ProactiveAlert -> OnProactiveAlert, ToolResult -> OnGeneralChat, Error -> Debug.WriteLine + top strip. No new timeline event types needed.
- **Error type surfaces via top strip, not timeline:** Brain pipeline errors are operational, not game events. They appear in the status strip but don't clutter the timeline.
- **Fire-and-forget StopConsuming:** The consumer task has its own exception handling, so StopConsuming cancels and disposes without awaiting. Safe to call multiple times.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Next Phase Readiness
- BrainEventRouter ready for Plan 04 to wire IBrainService.Results into StartConsuming
- Dual ingestion paths (direct-call + Channel) ensure backward compatibility during migration
- No blockers or concerns

---
*Phase: 05-brain-infrastructure*
*Completed: 2026-03-02*
