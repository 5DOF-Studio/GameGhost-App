# Phase 05: Brain Infrastructure — Research Summary

**Date:** March 2, 2026
**Status:** Research Complete
**Sources:**
- `GAIMER/GameGhost/gaimer_spec_docs/openrouter-integration-research.md`
- `GAIMER/GameGhost/gaimer_spec_docs/voice-brain-concurrency-research.md`
- `GAIMER/GameGhost/gaimer_spec_docs/brain-and-tools-reference.md`
- `GAIMER/GameGhost/gaimer_spec_docs/chat-brain-design.md`

---

## Phase Goal

IBrainService + OpenRouter REST client for brain/worker LLM calls. BrainResult types, Channel<T> pipeline, tool execution runtime (capture_screen, analyze_board, internet_search). Separate brain from voice provider.

---

## Architecture Decision: Dual Voice + Brain Pipeline

The current single `IConversationProvider` handles both voice and analytical work. Phase 05 adds a parallel `IBrainService` for REST-based LLM analysis that runs independently of voice.

```
                    ┌────────────────────┐
                    │   Screen Capture   │
                    └────────┬───────────┘
                             │ image bytes
                             ▼
┌──────────────────────────────────────────────┐
│           IBrainService (NEW)                 │
│  - REST/SSE LLM (via OpenRouter)              │
│  - Receives images, executes tools            │
│  - Runs on background threads                 │
│  - Publishes results to Channel<BrainResult>  │
└─────────────────┬────────────────────────────┘
                  │ Channel<BrainResult>
                  ▼
┌──────────────────────────────────────────────┐
│       BrainEventRouter (UPGRADED)             │
│  - Reads from Channel<BrainResult>            │
│  - Routes to: Timeline, Voice, Ghost Mode     │
│  - Respects priority: INTERRUPT / WHEN_IDLE   │
└──────┬──────────────────┬────────────────────┘
       │                  │
       ▼                  ▼
┌──────────────┐  ┌────────────────────────┐
│ ITimelineFeed│  │ IConversationProvider   │
│ (unchanged)  │  │ (VOICE — unchanged)    │
└──────────────┘  └────────────────────────┘
```

---

## Key Interface: IBrainService

```csharp
public interface IBrainService : IDisposable
{
    Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default);
    Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default);
    ChannelReader<BrainResult> Results { get; }
    void CancelAll();
    bool IsBusy { get; }
}
```

---

## Key Type: BrainResult

```csharp
public record BrainResult
{
    public required BrainResultType Type { get; init; }
    public BrainHint? Hint { get; init; }
    public string? AnalysisText { get; init; }
    public string? VoiceNarration { get; init; }
    public BrainResultPriority Priority { get; init; }
    public string? CorrelationId { get; init; }
}

public enum BrainResultType { ImageAnalysis, ToolResult, ProactiveAlert, Error }
public enum BrainResultPriority { Interrupt, WhenIdle, Silent }
```

---

## OpenRouter Integration

- Base URL: `https://openrouter.ai/api/v1`
- OpenAI-compatible REST API (chat completions)
- Supports: streaming (SSE), tool/function calling, vision (base64), JSON mode
- Use `HttpClient` + `System.Text.Json` source generators
- Auth: `Authorization: Bearer sk-or-...` header
- Model selection at request time via `model` field

**Model tiers:**
| Role | Models |
|------|--------|
| Brain (analysis) | `anthropic/claude-sonnet-4-20250514`, `openai/gpt-4o`, `google/gemini-2.0-flash` |
| Worker (fast) | `openai/gpt-4o-mini`, `anthropic/claude-haiku-4-5-20251001` |

---

## Channel<T> Pipeline

`System.Threading.Channels.Channel<T>` for brain-to-router bridge:
- Bounded(32) with DropOldest
- SingleReader=true (BrainEventRouter), SingleWriter=false (concurrent analyses)
- BrainEventRouter gains background consumption loop via `ReadAllAsync()`

---

## Tool Execution Runtime

Existing `ToolDefinition` models are data-only. Phase 05 adds execution:
- `capture_screen` — triggers WindowCaptureService snapshot
- `analyze_board` — vision LLM analysis of captured screen
- `get_game_state` — returns SessionContext + BrainHint cache
- `get_best_move` — returns cached analysis (chess Brain pipeline)
- `web_search` — internet search for game info

Tool calls arrive in OpenAI format (`tool_calls` array). Brain service executes locally and returns results to LLM for multi-turn tool use.

---

## BrainEventRouter Upgrade

Current: synchronous method calls from MainViewModel
New: adds async Channel<BrainResult> consumer loop alongside existing direct calls

```csharp
private async Task ConsumeBrainResultsAsync(ChannelReader<BrainResult> reader, CancellationToken ct)
{
    await foreach (var result in reader.ReadAllAsync(ct))
    {
        RouteToTimeline(result);
        if (_voiceAgent?.IsConnected == true && result.VoiceNarration != null)
        {
            switch (result.Priority)
            {
                case BrainResultPriority.Interrupt:
                    await _voiceAgent.SendContextualUpdateAsync($"[URGENT] {result.VoiceNarration}", ct);
                    break;
                case BrainResultPriority.WhenIdle:
                    await _voiceAgent.SendContextualUpdateAsync(result.VoiceNarration, ct);
                    break;
            }
        }
    }
}
```

---

## Threading Model

| Component | Thread | Notes |
|-----------|--------|-------|
| Voice WebSocket receive | Background (Task.Run) | Already implemented |
| Brain REST requests | Background (Task.Run) | Per-request, fire-and-forget |
| Channel consumption | Background (BrainEventRouter loop) | Single reader |
| UI updates | MainThread | Via MainThread.BeginInvokeOnMainThread |

Cancellation: Session CTS -> Brain global CTS -> per-request linked CTS

---

## Existing Code to Modify

| File | Action |
|------|--------|
| `Services/BrainEventRouter.cs` | MODIFY — add Channel consumer loop |
| `Services/IBrainEventRouter.cs` | MODIFY — add StartConsuming method |
| `ViewModels/MainViewModel.cs` | MODIFY — inject IBrainService, redirect captures |
| `MauiProgram.cs` | MODIFY — register IBrainService, OpenRouter config |

## New Files to Create

| File | Purpose |
|------|---------|
| `Services/IBrainService.cs` | Brain pipeline interface |
| `Services/Brain/OpenRouterBrainService.cs` | REST implementation via OpenRouter |
| `Services/Brain/OpenRouterClient.cs` | HTTP client wrapper for OpenRouter API |
| `Services/Brain/ToolExecutor.cs` | Local tool execution runtime |
| `Services/Brain/MockBrainService.cs` | Mock for development/testing |
| `Models/BrainResult.cs` | Channel message type |
| `Models/OpenRouterModels.cs` | Request/response DTOs with STJ source gen |

---

## Race Condition Handling

- **Correlation ID**: Each brain request gets unique ID. Stale results downgraded to Silent.
- **Debounce**: 2-3 second window after last move before brain analyzes.
- **Latest-wins**: New image submission cancels previous in-flight analysis.
