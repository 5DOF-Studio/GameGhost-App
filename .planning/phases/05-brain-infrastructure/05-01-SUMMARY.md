---
phase: 05-brain-infrastructure
plan: 01
subsystem: brain-pipeline
tags: [channel, brain-result, openrouter, system-text-json, source-generator, mock-service]

# Dependency graph
requires:
  - phase: 02-chat-brain
    provides: BrainHint, SharedContextEnvelope, BrainEventRouter, ChatMessage models
provides:
  - BrainResult record (channel message type for brain pipeline)
  - BrainResultType and BrainResultPriority enums
  - OpenRouter request/response/streaming DTOs with STJ source generators
  - IBrainService interface (ChannelReader<BrainResult> contract)
  - MockBrainService (dev/testing without API key)
affects:
  - 05-02 (OpenRouterClient uses OpenRouterModels DTOs)
  - 05-03 (BrainEventRouter Channel upgrade reads from IBrainService.Results)
  - 05-04 (DI wiring registers IBrainService)
  - 06-polish (error handling, testing)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Channel<BrainResult> bounded(32, DropOldest, SingleReader) for brain-to-router pipeline"
    - "STJ source generators with SnakeCaseLower and WhenWritingNull for OpenRouter API"
    - "Fire-and-forget Task.Run with linked CTS for async brain submissions"
    - "ContentConverter for polymorphic JSON (string or List<ContentPart> in OpenAI format)"

key-files:
  created:
    - src/GaimerDesktop/GaimerDesktop/Models/BrainResult.cs
    - src/GaimerDesktop/GaimerDesktop/Models/OpenRouterModels.cs
    - src/GaimerDesktop/GaimerDesktop/Services/IBrainService.cs
    - src/GaimerDesktop/GaimerDesktop/Services/Brain/MockBrainService.cs
  modified: []

key-decisions:
  - "BrainResult as C# record (immutable, value equality) rather than class"
  - "CorrelationId for stale detection when new images supersede old analyses"
  - "CreatedAt defaults to DateTimeOffset.UtcNow for pipeline latency tracking"
  - "ContentConverter custom JsonConverter for polymorphic Content field (string or vision parts)"
  - "Bounded channel DropOldest prevents backpressure from blocking analysis"

patterns-established:
  - "IBrainService: fire-and-forget submit, async channel consumption"
  - "MockBrainService: rotating mock data arrays for realistic demo behavior"
  - "Services/Brain/ subfolder for brain pipeline implementations"

# Metrics
duration: 7min
completed: 2026-03-02
---

# Phase 05 Plan 01: Foundation Types Summary

**BrainResult record, OpenRouter DTOs with STJ source generators, IBrainService Channel interface, and MockBrainService for keyless development**

## Performance

- **Duration:** 7 min
- **Started:** 2026-03-02T17:37:37Z
- **Completed:** 2026-03-02T17:44:12Z
- **Tasks:** 2
- **Files created:** 4

## Accomplishments
- BrainResult record with 7 fields covering all brain pipeline output categories (ImageAnalysis, ToolResult, ProactiveAlert, Error)
- OpenRouter request/response/streaming DTOs with STJ source generator context (SnakeCaseLower, WhenWritingNull, polymorphic Content via custom converter)
- IBrainService interface with ChannelReader<BrainResult> for downstream consumption by BrainEventRouter
- MockBrainService with bounded Channel(32), fire-and-forget async writes, linked CTS cancellation, and 5 rotating chess-themed mock analyses

## Task Commits

Each task was committed atomically:

1. **Task 1: BrainResult types + OpenRouter DTOs** - `2d58a21` (feat)
2. **Task 2: IBrainService interface + MockBrainService** - `6cf86b6` (feat)

## Files Created
- `Models/BrainResult.cs` - BrainResult record, BrainResultType enum, BrainResultPriority enum
- `Models/OpenRouterModels.cs` - OpenRouter chat completion DTOs (request, response, streaming, tools) with STJ source generator context and ContentConverter
- `Services/IBrainService.cs` - Brain pipeline interface: SubmitImageAsync, SubmitQueryAsync, ChannelReader<BrainResult> Results, CancelAll, IsBusy, ProviderName
- `Services/Brain/MockBrainService.cs` - Mock implementation with bounded channel, simulated latency, rotating mock analyses, proper cancellation and disposal

## Decisions Made
- **BrainResult as record:** Immutable value type with `required` modifier on Type field. Records provide value equality and with-expression support for the pipeline.
- **CorrelationId field:** Enables stale result detection when a new image is submitted before the previous analysis completes. Latest-wins cancellation uses this for tracking.
- **CreatedAt with DateTimeOffset:** Provides timezone-aware timestamps for pipeline latency measurement, defaulting to UtcNow at creation.
- **Custom ContentConverter:** OpenAI/OpenRouter Content field is polymorphic (string for text, array for vision). Custom JsonConverter handles both during deserialization without losing type safety.
- **Channel DropOldest policy:** If BrainEventRouter falls behind (32 results queued), oldest results are dropped rather than blocking new analyses. Matches gaming use case where latest analysis matters most.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required. MockBrainService operates without an API key.

## Next Phase Readiness
- Foundation types ready for Plan 02 (OpenRouterClient) to implement HTTP calls against OpenRouter API
- IBrainService interface ready for Plan 03 (BrainEventRouter Channel upgrade) to consume Results
- MockBrainService ready for Plan 04 (DI wiring) to register as fallback when no API key is configured
- No blockers or concerns

---
*Phase: 05-brain-infrastructure*
*Completed: 2026-03-02*
