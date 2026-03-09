# Brain Context Pipeline Spec (Voice + Chat + Reels)

**Project:** Gaimer Desktop  
**Purpose:** Define how the brain module captures game context, summarizes events over time, and serves low-latency context to voice/chat pipelines.  
**Status:** Draft for implementation  
**Primary Consumers:** Voice pipeline, chat pipeline, provider adapters

---

## 1) Problem Statement

The UI now supports chat and live coaching experiences, but backend context is not yet consistently structured or queryable.  
This spec defines a **time-aware brain module** that:

- Ingests capture events and interaction events.
- Maintains three layers of context memory.
- Produces budgeted context packets on request.
- Supports optional critical push alerts.

Goal: **low-latency, reliable, and coherent AI responses** without replaying raw capture for each turn.

---

## 2) Core Design Principles

- **Pull-first model:** Voice/chat request context explicitly (`GetContextForVoice` / `GetContextForChat`).
- **Push-on-critical model:** Brain emits high-priority alerts when risk thresholds are crossed.
- **Multi-layer memory:** Separate immediate facts from rolling summary and long session narrative.
- **Deterministic truncation:** Hard token budgets with stable priority order.
- **Confidence-aware context:** Every event/summary carries confidence + staleness metadata.

---

## 3) Three-Layer Memory Model

### L1 - Immediate Window (0-30s)
- High-fidelity, short-lived events.
- Used for immediate reactions and tactical callouts.
- Examples:
  - "Enemy entered left corridor at T-6s"
  - "Player HP dropped from 70 to 20 at T-2s"

### L2 - Rolling Summary (30s-5m)
- Chunked timeline summaries (e.g., 15s/30s buckets).
- Used for short-term trend guidance.
- Examples:
  - "Repeated overpeek pattern in last 2 minutes"
  - "Control of objective lost twice, regained once"

### L3 - Session Narrative (5m+)
- Strategic and behavioral narrative over session duration.
- Used for coaching continuity and adaptation.
- Examples:
  - "Fails after early advantage due to overextension"
  - "Improved rotations when pinging before moving"

---

## 4) Data Contracts

## 4.1 Event DTOs

```csharp
public enum BrainEventType
{
    VisionObservation,
    GameplayState,
    UserAction,
    SystemSignal
}

public sealed class BrainEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; init; }
    public BrainEventType Type { get; init; }
    public string Category { get; init; } = string.Empty; // e.g. threat, objective, economy
    public string Text { get; init; } = string.Empty;
    public double Confidence { get; init; } // 0..1
    public TimeSpan? ValidFor { get; init; } // optional staleness window
    public Dictionary<string, string>? Tags { get; init; } // map, mode, team, etc.
}
```

## 4.2 Reel DTOs

```csharp
public sealed class ReelMoment
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; init; }
    public string SourceTarget { get; init; } = string.Empty; // process/window
    public string? FrameRef { get; init; } // local pointer/path/key
    public string? EventRef { get; init; } // links to BrainEvent.Id
    public double Confidence { get; init; } // extraction confidence if generated
}
```

## 4.3 Shared Context Envelope

```csharp
public sealed class SharedContextEnvelope
{
    public DateTime RequestTsUtc { get; init; }
    public string Intent { get; init; } = "general"; // tactical, explanation, recap
    public int BudgetTokens { get; init; }

    public IReadOnlyList<BrainEvent> ImmediateEvents { get; init; } = [];
    public string RollingSummary { get; init; } = string.Empty;
    public string? SessionNarrative { get; init; }

    public IReadOnlyList<ReelMoment> ReelRefs { get; init; } = [];
    public string TruncationReport { get; init; } = string.Empty;
    public double EnvelopeConfidence { get; init; } // 0..1
}
```

## 4.4 Critical Alert DTO (Push Path)

```csharp
public sealed class CriticalAlert
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime TimestampUtc { get; init; }
    public string Severity { get; init; } = "high"; // high/critical
    public string Message { get; init; } = string.Empty;
    public string ReasonCode { get; init; } = string.Empty;
    public double Confidence { get; init; }
}
```

---

## 5) Brain Module Interfaces

```csharp
public interface IBrainContextService
{
    Task IngestEventAsync(BrainEvent evt, CancellationToken ct = default);
    Task IngestReelMomentAsync(ReelMoment moment, CancellationToken ct = default);

    Task<SharedContextEnvelope> GetContextForVoiceAsync(
        DateTime requestTsUtc,
        string intent,
        int budgetTokens,
        CancellationToken ct = default);

    Task<SharedContextEnvelope> GetContextForChatAsync(
        DateTime requestTsUtc,
        string intent,
        int budgetTokens,
        CancellationToken ct = default);

    event EventHandler<CriticalAlert>? CriticalAlertRaised;
}
```

---

## 6) Context Assembly Algorithm

1. Fetch L1 events in `[requestTs - 30s, requestTs]`.
2. Filter low-confidence and stale events.
3. Fetch nearest reel refs linked to selected L1 events.
4. Fetch latest L2 summary chunks for past 5 minutes.
5. Fetch L3 narrative if budget allows.
6. Apply deterministic token budget:
   - Keep newest L1 first.
   - Keep threat/objective categories before cosmetic categories.
   - Keep reel refs with highest confidence first.
   - Drop L3 first when budget pressure is high.
7. Return envelope + truncation report.

---

## 7) Token Budget Policy

- Default voice budget: `900` tokens.
- Default chat budget: `1200` tokens.
- Hard max: `1600` tokens.

Priority order under truncation:
1. L1 immediate events (high confidence only)
2. L2 rolling summary (latest chunk)
3. Reel refs
4. L3 session narrative

Always include:
- Active target/game metadata
- Last user request intent
- At least 1 L1 event if available

---

## 8) Push vs Pull Behavior

## Pull (Default)
- Voice/chat asks brain for envelope when a turn starts.
- Brain responds with bounded context packet.

## Push (Critical Only)
- Brain raises `CriticalAlertRaised` for high-risk conditions:
  - Sudden threat spike
  - Objective critical state change
  - Imminent failure condition
- Voice pipeline may inject concise alert even before user asks.

Push guardrails:
- Debounce duplicate alerts within 3s window.
- Suppress if confidence < configured threshold.

---

## 9) Failure Handling

- If brain unavailable: fallback to minimal context (`last user turn + static agent prompt`).
- If summary generation fails: continue with L1 + reel refs only.
- If reel lookup fails: context still returned with event text only.
- If budgeting fails: return safe minimal envelope and log truncation failure.

All failures should emit structured system messages to chat pipeline.

---

## 10) Observability and Metrics

Track per session:
- `brain_context_build_latency_ms` (p50/p95)
- `events_ingested_count`
- `reel_moments_ingested_count`
- `context_truncation_rate`
- `critical_alerts_emitted_count`
- `critical_alerts_suppressed_count`
- `envelope_confidence_avg`

Performance targets:
- Context build latency: p95 < 120ms
- Critical alert debounce false-positive rate < 5%

---

## 11) Security and Privacy

- Keep reel artifacts local by default.
- Do not include raw frame payloads in context envelopes; only refs and summaries.
- Redact known sensitive window data when target category is non-gaming/work-sensitive.

---

## 12) Implementation Plan

### Phase A (Sprint 1)
- Implement DTOs and `IBrainContextService`.
- Implement L1 event store + simple L2 rolling summarizer.
- Implement `GetContextForVoiceAsync` with budget enforcement.
- Wire chat/voice request path to pull context envelopes.

### Phase B (Sprint 2)
- Add L3 session narrative summarizer.
- Add critical push alerts with debounce.
- Add richer confidence scoring.
- Add metrics and dashboards/log sinks.

---

## 13) Acceptance Criteria

- Voice request receives context packet with L1 + L2 in under 120ms p95.
- Chat request receives packet with deterministic truncation report.
- Critical alert path emits no duplicate alerts within debounce window.
- Context envelopes include confidence and staleness metadata.
- System remains functional when summarizer or reel query fails.

---

## 14) Open Decisions

- Final thresholds for critical alert categories by game mode.
- Retention duration for L1/L2/L3 memory stores.
- Whether L3 summaries are regenerated or incrementally appended.

