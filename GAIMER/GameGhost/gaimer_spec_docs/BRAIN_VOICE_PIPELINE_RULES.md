# Brain-Voice Pipeline Architecture Rules

**Project:** Gaimer Desktop
**Status:** CANONICAL — This document defines the system's core architectural boundary.
**Effective:** March 2, 2026
**Supersedes:** Any prior flow where voice receives raw image data.

---

## The Golden Rule

> **The Brain is the sole consumer of visual data. Voice never sees raw images.**

Everything downstream of the brain — voice, chat, timeline, ghost mode — receives processed text, structured metadata, and priority-tagged narrations. Never image bytes.

This is not a suggestion. It is the foundational architectural constraint that makes the entire system work.

---

## 1. The Pipeline

```
Screen Capture (30s interval or on-demand)
         │
         │  JPEG/PNG bytes
         ▼
┌─────────────────────────────────────────┐
│          IBrainService (SOLE CONSUMER)  │
│                                         │
│  Receives: image bytes + game context   │
│  Produces: BrainResult (structured)     │
│  Method:   SubmitImageAsync()           │
│                                         │
│  Internally:                            │
│  - Sends image to vision LLM (OpenRouter)
│  - Runs multi-turn tool calls if needed │
│  - Generates analysis text              │
│  - Generates voice narration (text)     │
│  - Assigns priority (Interrupt/WhenIdle/Silent)
│  - Publishes BrainResult to Channel     │
└────────────────┬────────────────────────┘
                 │
                 │  Channel<BrainResult>
                 ▼
┌─────────────────────────────────────────┐
│      BrainEventRouter (DISTRIBUTOR)     │
│                                         │
│  Reads from Channel in background loop  │
│  Routes each BrainResult to:            │
│                                         │
│  1. Timeline (ALWAYS)                   │
│     → Persistent event record           │
│     → L1 immediate event for context    │
│                                         │
│  2. Voice (CONDITIONAL)                 │
│     → Only if voice connected           │
│     → Only if priority != Silent        │
│     → Text only via SendContextualUpdate│
│     → Interrupt: [URGENT] prefix        │
│     → WhenIdle: normal delivery         │
│                                         │
│  3. Ghost Mode (CONDITIONAL)            │
│     → Only if ghost mode active         │
│     → Card display with metadata        │
│                                         │
│  4. Context Service (ALWAYS)            │
│     → L1 event ingestion                │
│     → L2 rolling summary update         │
│     → L3 narrative accumulation         │
└─────────────────────────────────────────┘
```

---

## 2. What Voice Receives

Voice is a **conversational agent**. It speaks, listens, and responds. It does NOT process images.

### Voice gets text only, via two paths:

**Path A — Push (from Brain via Router):**
```
BrainResult.VoiceNarration → SendContextualUpdateAsync(text)
```
Brain discovers something → router pushes a text string to voice → voice decides whether to speak based on its system prompt rules.

**Path B — Pull (from Context Service):**
```
GetContextForVoiceAsync() → SharedContextEnvelope → text summary
```
Voice starting a conversation turn → requests context from brain context service → receives a budget-truncated text packet with L1 events + L2 summary + L3 narrative.

### Voice NEVER receives:
- Raw image bytes
- Base64-encoded frames
- Screenshot references
- Direct capture events

---

## 3. What Voice Can Request

Voice has access to tools that **read from the brain's state**, not from capture directly:

| Tool | What It Does | Latency |
|------|-------------|---------|
| `get_game_state` | Reads ISessionManager context (state, game, tools) | <1ms (cache read) |
| `get_best_move` | Reads brain's latest move analysis | <1ms (cache read) |
| `capture_screen` | **Triggers brain** to snap + analyze a new frame | 2-5s (LLM call) |

When voice calls `capture_screen`, it does NOT receive raw image bytes back. The flow is:

```
Voice tool call: capture_screen
    │
    ▼
Brain.SubmitImageAsync(fresh_capture)
    │
    ▼ (2-5 seconds)
BrainResult published to Channel
    │
    ▼
BrainEventRouter routes:
    → Timeline: new ImageAnalysis event
    → Voice: "I see a Sicilian Defense setup. Black has strong pawn control..."
    → Ghost: card with analysis summary
```

The voice agent receives the analysis as text narration, not as image data.

---

## 4. The Three-Layer Context Model

Brain maintains a time-layered memory system that any consumer can query:

### L1 — Immediate Window (0-30 seconds)
- Individual observations from the latest brain analysis
- High-fidelity, short-lived
- Examples: "Knight moved to f6", "Board evaluation shifted +150cp"
- Source: Each BrainResult generates L1 events

### L2 — Rolling Summary (30 seconds to 5 minutes)
- Chunked summaries of recent game behavior
- Regenerated on a rolling basis as new L1 events arrive
- Examples: "Player has been trading pieces aggressively", "Pawn structure weakened on kingside"
- Source: Periodic summarization of L1 events by the brain

### L3 — Session Narrative (5+ minutes)
- Strategic arc of the entire session
- Used for coaching continuity
- Examples: "Player started strong but is losing momentum after move 20"
- Source: Incremental updates as L2 summaries accumulate

### Budget-Truncated Context Packets
When voice or chat requests context, the system assembles a `SharedContextEnvelope` from these layers, truncated to a token budget:
- Voice budget: 900 tokens (optimized for brevity)
- Chat budget: 1200 tokens (more detailed)
- Hard max: 1600 tokens
- Truncation order: Drop L3 first, then reel refs, then older L2 chunks

---

## 5. Push vs Pull

### Pull (Default — Consumer-Initiated)
Consumer asks for context when it needs it:
```csharp
// Voice starting a turn
var envelope = await _brainContext.GetContextForVoiceAsync(
    DateTime.UtcNow, "tactical", budgetTokens: 900, ct);
```

### Push (Critical — Brain-Initiated)
Brain detects high-urgency condition → pushes to voice immediately:
```csharp
// Brain detects blunder
BrainResult {
    Type = ProactiveAlert,
    Priority = Interrupt,          // Voice speaks immediately
    VoiceNarration = "Wait — that move loses the queen!",
    Hint = { Signal = "blunder", Urgency = "critical" }
}
```

Push guardrails:
- 3-second debounce between alerts
- Confidence threshold suppression
- Only Interrupt and WhenIdle priorities trigger voice

---

## 6. The Capture Precepts

How frames flow into the brain:

### Auto Precept (Default)
Timer-based capture every 30 seconds. Brain analyzes if not busy:
```
Timer tick → capture → Brain.SubmitImageAsync() (if !IsBusy)
```

### Diff Precept (Phase 06)
IFrameDiffService detects board-state changes via dHash:
```
Timer tick → capture → dHash compare → if changed: Brain.SubmitImageAsync()
```
Reduces unnecessary API calls when nothing has changed.

### On-Demand Precept
User or voice tool requests a fresh capture:
```
Voice: "What's happening?"
  → capture_screen tool triggered
  → Brain.SubmitImageAsync(fresh_frame)
  → Result flows through Channel → Router → Voice
```

---

## 7. Anti-Patterns (PROHIBITED)

These patterns violate the architecture and must never be implemented:

### 7.1 Voice receives raw images
```csharp
// WRONG — voice should never see image bytes
_conversationProvider.SendImageAsync(compressed);
```
This was a legacy pattern from before the brain pipeline existed. It must be removed.

### 7.2 Duplicate analysis paths
```csharp
// WRONG — only brain analyzes images
_conversationProvider.SendImageAsync(compressed);  // voice analyzes
_brainService.SubmitImageAsync(compressed, ctx);   // brain also analyzes
```
Two different systems analyzing the same image produces conflicting interpretations and wastes API calls.

### 7.3 Voice calls LLM directly for image analysis
```csharp
// WRONG — voice is conversational, not analytical
var analysis = await _voiceProvider.AnalyzeImage(imageBytes);
```
Voice should request brain context, not run its own vision pipeline.

### 7.4 Bypassing the Channel
```csharp
// WRONG — all brain output must flow through Channel → Router
var result = await _brainService.AnalyzeDirectly(image);
_timeline.AddEvent(result);  // direct timeline write, bypassing router
```
The Channel is the single source of truth for brain output distribution.

### 7.5 Sending full BrainResult to voice
```csharp
// WRONG — voice receives text narration, not structured data
await _voiceAgent.SendContextualUpdateAsync(JsonSerializer.Serialize(brainResult));
```
Voice receives `VoiceNarration` (natural language text), not serialized objects.

---

## 8. End-to-End Scenarios

### Scenario A: Periodic capture during gameplay
```
1. Timer fires (30s)
2. WindowCaptureService captures frame
3. Frame compressed via ScaleAndCompress
4. Brain.SubmitImageAsync(compressed, gameContext)
5. Brain sends to OpenRouter vision model
6. LLM returns: "Black's knight is well-placed on f6..."
7. BrainResult(ImageAnalysis, text, narration, WhenIdle)
8. Channel.WriteAsync(result)
9. Router reads result:
   a. Timeline: new ImageAnalysis event added
   b. Voice: SendContextualUpdateAsync("Knight on f6 looks strong...")
   c. Ghost: card appears with analysis
10. Voice agent decides: speak the narration or hold for next user turn
```

### Scenario B: User asks "What should I play?"
```
1. Voice hears question, responds: "Let me look at the position..."
2. MainViewModel detects analytical intent
3. Brain.SubmitQueryAsync(query, contextEnvelope)
   OR voice tool call → capture_screen → Brain.SubmitImageAsync(fresh)
4. Brain runs multi-turn:
   a. Vision analysis of position
   b. get_game_state tool → session context
   c. get_best_move tool → (placeholder, brain uses own analysis)
5. BrainResult(ToolResult, analysis, narration, WhenIdle)
6. Channel → Router → Voice: "I'd recommend knight to f3, attacking the center"
7. Voice speaks the recommendation naturally
```

### Scenario C: Brain detects critical blunder
```
1. Periodic capture analyzed
2. Brain evaluation: delta = -500cp (massive shift)
3. BrainResult(ProactiveAlert, hint={blunder}, narration, Interrupt)
4. Channel → Router:
   a. Timeline: blunder alert event with urgency=critical
   b. Voice: SendContextualUpdateAsync("[URGENT] Wait — that move loses material!")
   c. Ghost: card with red urgency indicator
5. Voice interrupts current conversation to warn player
```

### Scenario D: Nothing changed (Diff Precept)
```
1. Timer fires (30s)
2. WindowCaptureService captures frame
3. IFrameDiffService.HasChanged(frame) → false (dHash match)
4. Frame discarded — brain NOT called
5. No API cost, no timeline noise
```

---

## 9. State Ownership

Each service owns its domain. No shared mutable state.

```
IWindowCaptureService  → owns → frame capture, target window
IBrainService          → owns → LLM analysis, tool execution, Channel<BrainResult>
IBrainContextService   → owns → L1/L2/L3 memory, SharedContextEnvelope assembly
BrainEventRouter       → owns → result distribution to consumers
IConversationProvider   → owns → voice WebSocket, audio I/O
ISessionManager        → owns → game state, session lifecycle
ITimelineFeed          → owns → persistent event timeline
IGhostModeService      → owns → native overlay rendering
```

---

## 10. Migration Notes

### Phase 05 Deviation (To Fix in Phase 06)
Phase 05 implemented the brain pipeline correctly BUT also left the legacy `SendImageAsync` call in the FrameCaptured handler. This means voice currently receives raw images alongside brain analysis — violating the golden rule.

**Phase 06 must:**
1. Remove `_conversationProvider.SendImageAsync(compressed)` from FrameCaptured
2. Ensure brain is the sole recipient of captured frames
3. Wire voice to receive brain output only via SendContextualUpdateAsync
4. Implement IFrameDiffService for smart frame submission
5. Build L1/L2 context layers in IBrainContextService

### V2 Migration (Local AI)
When swapping to local inference (MiniCPM-o via Ollama):
- Replace OpenRouterBrainService with OllamaBrainService
- Same IBrainService interface, same Channel, same Router
- Voice stays on its existing provider (or swaps to CosyVoice2)
- The pipeline boundary is preserved — only the LLM backend changes
