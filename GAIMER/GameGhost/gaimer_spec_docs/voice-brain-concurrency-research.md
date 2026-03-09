# Dual Voice + Brain Pipeline Architecture Research

**Date:** March 2, 2026
**Status:** Research Complete

---

## Executive Summary

The recommended architecture is a **Voice + Brain split** using two separate provider instances coordinated through a `Channel<T>`-based message bus, with the existing `BrainEventRouter` upgraded to serve as the handoff bridge. The voice provider stays on its existing WebSocket (Gemini Live / OpenAI Realtime) for bidirectional audio. The brain uses REST API calls for image analysis, tool execution, and multi-step reasoning.

---

## Current Architecture (Single Provider)

Today, a single `IConversationProvider` singleton handles both voice and analytical work:

```
User Speech → IConversationProvider → Voice + Image Analysis + Tools (all same connection)
```

**Problems:**
- Voice connection must do double duty as audio interface AND analytical engine
- No mechanism for parallel execution (voice blocks while brain thinks)
- Screen captures sent to voice provider which may not support images (OpenAI Realtime)
- No tool execution runtime (ToolDefinition models exist but are data-only)

---

## Recommended Architecture: Dual Pipeline

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
│              │  │ - WebSocket audio       │
│              │  │ - SendContextualUpdate  │
│              │  │ - NEVER processes images│
└──────────────┘  └────────────────────────┘
```

---

## Core Interface: IBrainService

```csharp
public interface IBrainService : IDisposable
{
    /// Submit an image for analysis. Returns immediately.
    /// Results arrive via the BrainResult channel.
    Task SubmitImageAsync(byte[] imageData, string context, CancellationToken ct = default);

    /// Submit a user query requiring tool execution.
    Task SubmitQueryAsync(string userQuery, SharedContextEnvelope context, CancellationToken ct = default);

    /// Channel reader for consuming brain results.
    ChannelReader<BrainResult> Results { get; }

    /// Cancel all in-flight analysis work.
    void CancelAll();

    /// True if the brain has work in progress.
    bool IsBusy { get; }
}
```

## Core Type: BrainResult

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

## Why Channel<T>

`System.Threading.Channels.Channel<T>` is ideal for the brain-to-voice bridge:

- **Async producer-consumer**: Brain produces on background threads; BrainEventRouter consumes
- **Backpressure**: Bounded channel with `DropOldest` prevents unbounded memory growth
- **Thread-safe**: No manual locking. Multiple concurrent brain analyses write to same channel
- **Cancellation**: `ChannelReader.ReadAllAsync(CancellationToken)` supports cooperative cancellation

```csharp
_channel = Channel.CreateBounded<BrainResult>(new BoundedChannelOptions(32)
{
    FullMode = BoundedChannelFullMode.DropOldest,
    SingleReader = true,    // Only BrainEventRouter reads
    SingleWriter = false    // Multiple analyses can complete concurrently
});
```

---

## BrainEventRouter Upgrade

The existing `BrainEventRouter` gains a background consumption loop:

```csharp
private async Task ConsumeBrainResultsAsync(ChannelReader<BrainResult> reader, CancellationToken ct)
{
    await foreach (var result in reader.ReadAllAsync(ct))
    {
        // Always route to timeline
        RouteToTimeline(result);

        // Route to voice based on priority
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
                case BrainResultPriority.Silent:
                    break; // Absorb into context only
            }
        }
    }
}
```

---

## End-to-End Flow Example

**User says "what should I play?":**

```
1. Voice hears audio → transcribes → responds "Let me take a look..."
   (Voice stays active, audio keeps flowing)

2. MainViewModel detects intent → calls _brainService.SubmitQueryAsync()

3. Brain executes on background thread:
   → capture_screen tool → JPEG bytes
   → analyze_board tool → vision LLM (2-5s)
   → get_best_move tool → chess engine (1-3s)
   → Publishes BrainResult to channel

4. BrainEventRouter reads result:
   → Routes to Timeline (new BestMove event)
   → Routes to Voice: "The best move is knight to f3..."
   → Routes to Ghost Mode (if active)

5. User can say "never mind" at any point:
   → _brainService.CancelAll() aborts all in-flight work
   → Voice responds: "No problem."
```

---

## Threading Model

| Component | Thread | Notes |
|-----------|--------|-------|
| Voice WebSocket receive | Background (Task.Run) | Already implemented |
| Brain REST requests | Background (Task.Run) | Per-request, fire-and-forget |
| Channel consumption | Background (BrainEventRouter loop) | Single reader |
| UI updates | MainThread | Via MainThread.BeginInvokeOnMainThread |
| Voice send path | Various threads | Needs SemaphoreSlim(1,1) for safety |

### Cancellation Hierarchy

```
Session CancellationTokenSource (_sessionCts)
├── Voice Provider (linked to session)
└── Brain Service (_globalCts)
    ├── Analysis Task 1 (linked to _globalCts + per-request ct)
    ├── Analysis Task 2
    └── Analysis Task 3
```

- "Never mind" → `_brainService.CancelAll()` (brain only)
- Session ends → `_sessionCts.Cancel()` (everything)

---

## Race Condition Handling

### Brain analyzing while user makes another move

1. **Correlation ID**: Each brain request gets unique ID. Router checks if game state advanced since request. Stale results downgraded to `Silent`.

2. **Debounce**: 2-3 second window after last move before brain analyzes. Prevents wasted work.

3. **Latest-wins**: New image submission cancels previous in-flight analysis:
```csharp
_currentAnalysisCts?.Cancel();
_currentAnalysisCts = CancellationTokenSource.CreateLinkedTokenSource(_globalCts.Token, ct);
```

---

## State Ownership (No Shared Mutable State)

```
SessionManager        → owns → SessionContext (state, gameId, gameType)
BrainContextService   → owns → SharedContextEnvelope (budgeted context)
IBrainService         → owns → Tool execution, produces BrainResult
IConversationProvider → owns → Voice interaction
BrainEventRouter      → owns → Result distribution
```

Each service reads from its authoritative source. No duplicate state. Brain receives context as a parameter, never maintains its own copy.

---

## Provider-Specific Notes

### Gemini Live: Potential Single-Provider Path
Gemini Live supports images + `NON_BLOCKING` function declarations. Could theoretically serve as both voice and brain:
- Send audio + images on same connection
- Tools declared with `behavior: NON_BLOCKING`
- Scheduling: `INTERRUPT` / `WHEN_IDLE` / `SILENT`

**Quick win** for Phase 1, but limits model flexibility.

### OpenAI Realtime: Must Use Dual Provider
Does NOT support image input. Separate brain mandatory for visual analysis. Voice stays on Realtime WebSocket; brain uses REST Chat Completions (GPT-4o with vision).

### Recommended Default: Dual Provider for Both
Decouples voice model from brain model. Allows independent scaling. Prevents voice WebSocket from being burdened with heavy payloads.

---

## Migration Path

| Phase | Change | Risk |
|-------|--------|------|
| 1 | Add `IBrainService` + `GeminiBrainService` alongside existing providers | None — additive |
| 2 | Redirect screen captures from voice to brain | Low — voice stops receiving images |
| 3 | Add voice-to-brain routing for user queries | Medium — new flow |
| 4 | Upgrade `BrainEventRouter` to async Channel consumer | Medium — replaces direct calls |

---

## Key Files to Create/Modify

| File | Action |
|------|--------|
| `Services/IBrainService.cs` | NEW — brain pipeline interface |
| `Services/Brain/OpenRouterBrainService.cs` | NEW — REST implementation via OpenRouter |
| `Models/BrainResult.cs` | NEW — channel message type |
| `Services/BrainEventRouter.cs` | MODIFY — add Channel consumer loop |
| `ViewModels/MainViewModel.cs` | MODIFY — inject IBrainService, redirect captures |
| `MauiProgram.cs` | MODIFY — register IBrainService |

---

## Industry References

- OpenAI Realtime API: async function calling (model continues conversation while waiting)
- Gemini Live API: NON_BLOCKING function behavior with INTERRUPT/WHEN_IDLE/SILENT scheduling
- LiveKit Agents: VoicePipelineAgent (STT→LLM→TTS) separate from MultimodalAgent
- Vapi: parallel audio/transcription/response streams with predict-and-scrap
- LTS-VoiceAgent: Foreground Speaker + Background Thinker + Dual-Role Stream Orchestrator
