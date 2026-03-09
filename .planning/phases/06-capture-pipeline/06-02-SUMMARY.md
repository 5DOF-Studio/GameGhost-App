---
phase: 06-capture-pipeline
plan: 02
subsystem: brain-context
tags: [brain-context, L1-events, L2-summary, token-budgeting, context-pipeline]

# Dependency graph
requires:
  - phase: 05-brain-infrastructure
    provides: BrainEvent model, SharedContextEnvelope, IBrainContextService (MVP), BrainContextService
provides:
  - IBrainContextService.IngestEventAsync for L1 event ingestion
  - L1 event store with 30s immediate window and 5min retention
  - L2 deterministic rolling summary (category-grouped text)
  - Budget-aware context envelopes with real ImmediateEvents and RollingSummary data
affects: [06-03 pipeline-enforcement, 07-persistence, voice-pipeline, chat-pipeline]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Lock-protected L1 event store with time-based pruning and count cap"
    - "Deterministic L2 summarization (category grouping, no LLM)"
    - "Priority-ordered token budgeting (target > L1 > chat > L2 > reel > L3)"
    - "Dynamic EnvelopeConfidence computed from L1/L2 event coverage"

key-files:
  created: []
  modified:
    - src/GaimerDesktop/GaimerDesktop/Services/IBrainContextService.cs
    - src/GaimerDesktop/GaimerDesktop/Services/BrainContextService.cs

key-decisions:
  - "Lock-based thread safety for L1 store (simple, low contention for gaming use case)"
  - "200 event cap + 5min retention window for L1 store bounds"
  - "High-priority categories (threat, objective, critical, danger, warning) budgeted first in L1"
  - "L2 summary is deterministic category-grouped text (no LLM call for MVP)"
  - "FormatAsPrefixedContextBlock rendering order aligned with budget priority (L1 before chat)"
  - "EnvelopeConfidence dynamically computed from L1/L2 coverage (0.5 base + event confidence + L1 bonus)"

patterns-established:
  - "L1 event ingestion: lock -> append -> prune by time -> cap by count"
  - "L2 summary: GroupBy category, OrderBy priority then count, format as 'category: N events. Latest: text'"
  - "Budget allocation cascade: allocate in priority order, track remaining, report truncation"

# Metrics
duration: 12min
completed: 2026-03-02
---

# Phase 6 Plan 2: L1/L2 Context Layers Summary

**IBrainContextService upgraded with L1 event store (30s window, lock-protected, 200 cap) and deterministic L2 rolling summary (category-grouped) replacing empty MVP stubs**

## Performance

- **Duration:** 12 min
- **Started:** 2026-03-02T20:48:37Z
- **Completed:** 2026-03-02T21:00:28Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- IBrainContextService gains IngestEventAsync method for L1 event ingestion from BrainEventRouter
- BrainContextService maintains thread-safe in-memory L1 event store with 5-minute retention and 200-event cap
- BuildEnvelope now populates ImmediateEvents from 0-30s window (confidence >= 0.3, staleness-filtered, priority-sorted)
- L2 rolling summary generated deterministically from 30s-5min window events grouped by category
- Token budget priority reordered: target > L1 > chat > L2 > reel refs > L3 (matching spec)
- FormatAsPrefixedContextBlock rendering order aligned with budget priority

## Task Commits

Each task was committed atomically:

1. **Task 1: Add IngestEventAsync to IBrainContextService** - `c26832e` (feat)
2. **Task 2: Upgrade BrainContextService with L1 event store and L2 rolling summary** - `907f540` (feat)

## Files Created/Modified
- `src/GaimerDesktop/GaimerDesktop/Services/IBrainContextService.cs` - Added IngestEventAsync(BrainEvent, CancellationToken) method signature
- `src/GaimerDesktop/GaimerDesktop/Services/BrainContextService.cs` - L1 event store, L2 summary generation, upgraded BuildEnvelope with priority budgeting, dynamic EnvelopeConfidence

## Decisions Made
- **Lock-based thread safety:** Simple object lock for L1 store -- low contention expected in 30s capture intervals. ConcurrentQueue was considered but List with lock gives better query flexibility for time-window filtering.
- **200 event cap + 5min retention:** Bounds memory growth. At 30s capture intervals, max ~10 events in L1 window and ~600 in retention -- 200 cap provides safety margin.
- **Priority categories budgeted first:** threat/objective/critical/danger/warning categories get budget priority in L1, matching spec requirement for tactical relevance.
- **Deterministic L2 (no LLM):** MVP approach -- groups events by category with count and latest text. Future enhancement can add LLM-based summarization.
- **FormatAsPrefixedContextBlock order change:** Moved "Immediate events" section before "Recent chat" to match budget priority order. Consumers see highest-priority context first.
- **Dynamic EnvelopeConfidence:** Previously hardcoded 0.9. Now computed from L1/L2 event coverage (0.5 base + confidence + L1 presence bonus) for more accurate downstream decisions.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] FormatAsPrefixedContextBlock rendering order mismatched budget priority**
- **Found during:** Task 2 (BrainContextService upgrade)
- **Issue:** Old rendering order showed "Recent chat" before "Immediate events", but budget priority puts L1 events before chat. This would confuse consumers parsing the text block.
- **Fix:** Reordered rendering to: Target > Immediate events > Recent chat > Rolling summary > Reel refs > Session narrative
- **Files modified:** BrainContextService.cs (FormatAsPrefixedContextBlock)
- **Verification:** Build succeeded, rendering order matches budget priority
- **Committed in:** 907f540 (Task 2 commit)

---

**Total deviations:** 1 auto-fixed (1 bug)
**Impact on plan:** Cosmetic fix aligning text rendering with budget priority order. No scope creep.

## Issues Encountered
- Transient MSB3883 file lock error on first build attempt (dotnet SDK ref assembly locked by another process). Resolved on retry -- no code changes needed.

## User Setup Required
None - no external service configuration required.

## Next Phase Readiness
- L1 event store and L2 rolling summary ready for consumption
- Plan 06-03 (Pipeline Enforcement) can now wire BrainEventRouter to call IngestEventAsync on each routed BrainResult
- Voice/chat pipelines will receive populated ImmediateEvents and RollingSummary in context envelopes
- L3 SessionNarrative remains null (Phase B per spec)

---
*Phase: 06-capture-pipeline*
*Completed: 2026-03-02*
