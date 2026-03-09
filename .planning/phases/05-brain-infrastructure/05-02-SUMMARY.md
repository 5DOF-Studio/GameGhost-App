---
phase: 05-brain-infrastructure
plan: 02
subsystem: brain-pipeline
tags: [openrouter, http-client, sse-streaming, tool-execution, vision, function-calling]

# Dependency graph
requires:
  - phase: 05-01
    provides: OpenRouterModels DTOs, BrainResult, IBrainService interface
provides:
  - OpenRouterClient (HTTP wrapper for OpenRouter REST API with sync/streaming/vision/tools)
  - OpenRouterException (diagnostic exception with StatusCode + ResponseBody)
  - ToolExecutor (local tool call execution runtime for brain LLM multi-turn)
affects:
  - 05-04 (DI wiring registers OpenRouterClient, ToolExecutor as dependencies for OpenRouterBrainService)
  - 06-polish (error handling, retry logic, rate limiting)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "OpenRouterClient: stateless HTTP wrapper using STJ source generators for serialization"
    - "IAsyncEnumerable SSE streaming with HttpCompletionOption.ResponseHeadersRead"
    - "TaskCompletionSource bridge from event-based IWindowCaptureService to async tool executor"
    - "JsonDocument.Parse for tool parameter schemas (OpenRouterFunction.Parameters as JsonElement)"

key-files:
  created:
    - src/GaimerDesktop/GaimerDesktop/Services/Brain/OpenRouterClient.cs
    - src/GaimerDesktop/GaimerDesktop/Services/Brain/ToolExecutor.cs
  modified: []

key-decisions:
  - "OpenRouterException as separate class with StatusCode + ResponseBody for API error diagnostics"
  - "No retry logic in client (caller responsibility) to keep client stateless and composable"
  - "ToolExecutor uses ILogger for observability on all tool dispatches and errors"
  - "capture_screen tool uses CancellationTokenSource.CreateLinkedTokenSource for timeout vs external cancellation"
  - "GetAvailableToolDefinitions delegates filtering to ISessionManager.GetAvailableTools (InGame/OutGame)"

patterns-established:
  - "OpenRouterClient: injected HttpClient, configures BaseAddress + headers in constructor"
  - "SSE parsing: line-by-line StreamReader, skip empty + non-data lines, deserialize data: prefix"
  - "Tool results as JSON strings for LLM multi-turn (not typed objects)"
  - "Placeholder tools return {status: not_available/not_implemented} for future integration"

# Metrics
duration: 21min
completed: 2026-03-02
---

# Phase 05 Plan 02: OpenRouter Client + Tool Executor Summary

**OpenRouterClient HTTP wrapper with sync/streaming/vision/tool completions, and ToolExecutor for local tool call dispatch via TaskCompletionSource bridge to capture service**

## Performance

- **Duration:** 21 min (includes transient filesystem I/O retries)
- **Started:** 2026-03-02T17:46:34Z
- **Completed:** 2026-03-02T18:07:24Z
- **Tasks:** 2
- **Files created:** 2

## Accomplishments

- **OpenRouterClient** with 4 public methods: ChatCompletionAsync (sync), ChatCompletionStreamAsync (SSE IAsyncEnumerable), CreateImageAnalysisRequest (base64 vision), CreateToolCallRequest (function calling). All serialization uses STJ source generators via OpenRouterJsonContext.Default.
- **OpenRouterException** with HttpStatusCode and raw ResponseBody for API error diagnostics.
- **ToolExecutor** with ExecuteToolAsync dispatching 5 tool cases: capture_screen (TaskCompletionSource bridge to FrameCaptured event with 5s timeout), get_game_state (reads SessionManager context), get_best_move (placeholder), web_search (placeholder), and unknown tool handler.
- **GetAvailableToolDefinitions** static helper mapping ToolDefinitions to OpenRouterTool format with JsonElement parameter schemas, filtered by session state via ISessionManager.

## Task Commits

Each task was committed atomically:

1. **Task 1: OpenRouter REST client** - `23b2d96` (pre-existing -- OpenRouterClient.cs was accidentally included in 05-03 docs commit by a prior session)
2. **Task 2: Tool execution runtime** - `d5b7080` (feat)

## Files Created

- `Services/Brain/OpenRouterClient.cs` - HTTP client wrapper for OpenRouter REST API. ChatCompletionAsync, ChatCompletionStreamAsync (SSE), CreateImageAnalysisRequest (vision), CreateToolCallRequest (tools). OpenRouterException for error handling.
- `Services/Brain/ToolExecutor.cs` - Local tool execution runtime. Dispatches capture_screen, get_game_state, get_best_move, web_search, and unknown tools. Returns JSON strings for LLM multi-turn. GetAvailableToolDefinitions maps to OpenRouter format.

## Decisions Made

- **OpenRouterException as separate public class:** Includes both HttpStatusCode and raw ResponseBody string for maximum diagnostic value when API calls fail. Message format: "OpenRouter API error (429 TooManyRequests): {body}".
- **No retry logic in OpenRouterClient:** Client is intentionally stateless per-request. Retry, backoff, and rate limiting are the caller's (OpenRouterBrainService) responsibility, keeping the HTTP layer composable.
- **ToolExecutor ILogger injection:** All tool dispatches are logged at Debug level, errors at Error level. Provides observability into which tools the LLM is calling and how often.
- **Linked CancellationTokenSource for capture_screen timeout:** Creates a linked CTS combining the external ct and a 5-second timeout. Distinguishes timeout (returns error JSON) from external cancellation (throws OperationCanceledException).
- **GetAvailableToolDefinitions delegates session filtering:** Calls ISessionManager.GetAvailableTools() which already applies InGame/OutGame filtering. No duplicate logic in ToolExecutor.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] ReadOnlySpan in async iterator method**
- **Found during:** Task 1
- **Issue:** Used `line.AsSpan(6)` and `data.SequenceEqual("[DONE]".AsSpan())` in `ChatCompletionStreamAsync` which is an `async IAsyncEnumerable` method. C# 12 (.NET 8) does not support ref types (`Span<T>`, `ReadOnlySpan<T>`) in async/iterator methods (CS8652).
- **Fix:** Replaced with `line.Substring(6)` and `data == "[DONE]"` -- equivalent behavior without ref types.
- **Files modified:** OpenRouterClient.cs
- **Commit:** Pre-existing (fix was applied before commit `23b2d96`)

**2. [Note] OpenRouterClient.cs pre-committed in wrong commit**
- **Found during:** Task 1 execution
- **Issue:** OpenRouterClient.cs was already tracked in HEAD, having been accidentally included in commit `23b2d96` (docs(05-03)) by a prior agent session. The file content matched exactly what Plan 05-02 specified.
- **Impact:** No new commit needed for Task 1. Task 2 (ToolExecutor) committed normally as `d5b7080`.

## Issues Encountered

- **Transient filesystem I/O timeouts:** macOS File Provider (iCloud) extended attributes caused "Operation timed out" errors on multiple source files and runtimeconfig.json during dotnet build. Resolved by retrying -- known behavior for projects in ~/Documents. No code changes needed.

## User Setup Required

None -- ToolExecutor uses placeholder implementations for get_best_move and web_search. OpenRouterClient requires API key at DI wiring time (Plan 05-04).

## Next Phase Readiness

- OpenRouterClient and ToolExecutor are ready for composition in Plan 05-04 (DI wiring + OpenRouterBrainService)
- OpenRouterClient correctly formats vision requests with base64 image content parts
- ToolExecutor returns JSON strings ready for OpenRouter function-calling multi-turn
- No blockers or concerns

---
*Phase: 05-brain-infrastructure*
*Completed: 2026-03-02*
