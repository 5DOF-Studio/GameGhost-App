---
phase: 05-brain-infrastructure
plan: 04
subsystem: brain-pipeline
tags: [openrouter, di-wiring, brain-service, maui-program, mainviewmodel, channel-pipeline, vision, tool-calling]

# Dependency graph
requires:
  - phase: 05-01
    provides: BrainResult, OpenRouterModels DTOs, IBrainService interface, MockBrainService
  - phase: 05-02
    provides: OpenRouterClient (HTTP wrapper), ToolExecutor (local tool dispatch)
  - phase: 05-03
    provides: BrainEventRouter Channel consumer (StartConsuming/StopConsuming/RouteBrainResult)
provides:
  - OpenRouterBrainService (production IBrainService with vision + multi-turn tool calling)
  - DI registration for IBrainService (config-based selection: OpenRouter or Mock)
  - MainViewModel brain pipeline integration (capture forwarding + channel consumer wiring)
  - Complete end-to-end brain pipeline: FrameCaptured -> SubmitImageAsync -> Channel -> RouteBrainResult -> Timeline/Voice
affects:
  - 06-polish (error handling, retry logic, rate limiting)
  - 07-persistence (brain results may be persisted to SQLite)
  - 08-polish (end-to-end testing, SubmitQueryAsync wiring for out-game chat)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Config-based IBrainService selection in DI: OpenRouterBrainService with API key, MockBrainService without"
    - "Parallel brain + voice pipelines: FrameCaptured sends to both IConversationProvider and IBrainService"
    - "App-lifetime Channel consumer (not session-scoped) survives session boundaries"
    - "IsBusy debounce prevents brain flooding from 30s capture timer"

key-files:
  created:
    - src/GaimerDesktop/GaimerDesktop/Services/Brain/OpenRouterBrainService.cs
  modified:
    - src/GaimerDesktop/GaimerDesktop/MauiProgram.cs
    - src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs

key-decisions:
  - "App-lifetime Channel consumer (not session-scoped) so brain results route even across sessions"
  - "Brain submission in else branch when voice disconnected -- brain can analyze without voice"
  - "IsBusy debounce prevents duplicate brain submissions on 30-second capture interval"
  - "CancelAll on session stop but no StopConsuming -- consumer loop survives session boundaries"
  - "OPENROUTER_APIKEY env var loaded via both prefix-stripped and unprefixed ConfigurationBuilder"
  - "ToolExecutor created in DI factory with ILogger from service provider"

patterns-established:
  - "Parallel pipeline: voice (IConversationProvider) + brain (IBrainService) both receive compressed captures"
  - "Config-gated DI: API key present -> production service, absent -> mock service"
  - "Brain lifecycle: CancelAll on disconnect, consumer survives, fresh CTS on reconnect"

# Metrics
duration: 23min
completed: 2026-03-02
---

# Phase 05 Plan 04: DI Wiring + End-to-End Pipeline Summary

**OpenRouterBrainService with vision analysis and multi-turn tool calling, DI registration with config-based mock/production selection, and MainViewModel integration for parallel brain+voice capture pipelines**

## Performance

- **Duration:** 23 min
- **Started:** 2026-03-02T18:11:31Z
- **Completed:** 2026-03-02T18:34:10Z
- **Tasks:** 2
- **Files created:** 1
- **Files modified:** 2

## Accomplishments

- **OpenRouterBrainService** -- full IBrainService implementation with vision-based image analysis (claude-sonnet-4-20250514), multi-turn tool calling (max 5 turns), text query support (gpt-4o-mini), Channel(32, DropOldest) result pipeline, linked CancellationTokenSource for composable cancellation, and sentence-boundary-aware voice narration truncation
- **DI registration** -- MauiProgram.cs selects OpenRouterBrainService when OPENROUTER_APIKEY environment variable is set, MockBrainService when absent or USE_MOCK_SERVICES is true. Same dev experience as before (no API key required for development)
- **MainViewModel integration** -- compressed captures submitted to brain service in parallel with voice provider, brain can analyze even without voice connection, IsBusy debounce prevents flooding, BrainEventRouter.StartConsuming wired as app-lifetime consumer, brain cancelled on session stop

## Task Commits

Each task was committed atomically:

1. **Task 1: OpenRouterBrainService implementation** - `10cf976` (feat)
2. **Task 2: DI registration + MainViewModel integration** - `4846b2f` (feat)

## Files Created/Modified

- `Services/Brain/OpenRouterBrainService.cs` - Production IBrainService: SubmitImageAsync with vision + tool loop, SubmitQueryAsync with worker model, Channel pipeline, CancelAll lifecycle, TruncateForVoice helper
- `MauiProgram.cs` - OPENROUTER_ env var loading, IBrainService DI registration (OpenRouter or Mock based on config), ToolExecutor factory with ILogger
- `ViewModels/MainViewModel.cs` - IBrainService field + constructor injection, brain submission in FrameCaptured handler (parallel to voice + else branch), StartConsuming wiring, CancelAll on session stop

## Decisions Made

| Decision | Rationale |
| --- | --- |
| App-lifetime Channel consumer | Brain results route to timeline/voice even across session boundaries |
| Brain submission in voice-disconnected else branch | Brain can analyze gameplay independently of voice agent |
| IsBusy debounce on capture timer | Prevents flooding brain with 30s captures if previous analysis not complete |
| CancelAll but no StopConsuming on disconnect | Consumer loop survives so reconnect doesn't need re-wiring |
| OPENROUTER_APIKEY via both prefix-stripped and unprefixed | Reliable env var lookup regardless of ConfigurationBuilder ordering |
| ToolExecutor created with ILogger from DI | Observability for tool dispatches in production |

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed character literal error in TruncateForVoice**
- **Found during:** Task 1 (OpenRouterBrainService implementation)
- **Issue:** Used `'. '` (char literal) instead of `". "` (string) in `LastIndexOf` call -- C# char literal cannot hold two characters
- **Fix:** Changed to `searchRegion.LastIndexOf(". ", StringComparison.Ordinal)`
- **Files modified:** Services/Brain/OpenRouterBrainService.cs
- **Verification:** Build succeeded with 0 errors
- **Committed in:** `10cf976` (part of Task 1 commit)

**2. [Rule 3 - Blocking] Added ILogger parameter to ToolExecutor DI creation**
- **Found during:** Task 2 (DI wiring)
- **Issue:** Plan's DI code omitted `ILogger<ToolExecutor>` constructor parameter that ToolExecutor requires
- **Fix:** Added `sp.GetRequiredService<ILogger<ToolExecutor>>()` to ToolExecutor factory in MauiProgram.cs
- **Files modified:** MauiProgram.cs
- **Verification:** Build succeeded with 0 errors
- **Committed in:** `4846b2f` (part of Task 2 commit)

---

**Total deviations:** 2 auto-fixed (1 bug, 1 blocking)
**Impact on plan:** Both auto-fixes necessary for compilation. No scope creep.

## Issues Encountered

None beyond the auto-fixed deviations above.

## User Setup Required

**External service configuration required for production brain pipeline.**

To use the real OpenRouter brain service (instead of MockBrainService):

1. Get an API key from [OpenRouter](https://openrouter.ai/keys)
2. Set the environment variable before running:
   ```bash
   export OPENROUTER_APIKEY=sk-or-v1-...
   ```
3. Verify DI console output shows: `IBrainService=OpenRouterBrainService (apiKeyPresent=true)`

Without the API key, the app runs normally with MockBrainService (rotating chess-themed demo analyses).

## Next Phase Readiness

- **Phase 05 complete** -- all 4 plans executed, brain pipeline end-to-end functional
- **Ready for Phase 06** -- capture pipeline with IFrameDiffService (dHash), capture precepts, BrainEventRouter upgrade
- **SubmitQueryAsync wiring deferred** -- out-game text chat brain query integration deferred to Phase 08 (Polish) per plan spec
- **No blockers** -- MockBrainService provides full dev/test experience without API key

---
*Phase: 05-brain-infrastructure*
*Completed: 2026-03-02*
