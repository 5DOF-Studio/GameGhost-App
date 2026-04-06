# Chess Barge-In Behavior Plan

**Status:** Proposed  
**Date:** 2026-03-16  
**Purpose:** Define how chess-specific unsolicited voice behavior should fit into the planned audio-intelligence / barge-in system without introducing a bespoke one-off runtime path.

## Decision

Do not implement a chess-only automatic speech path outside the audio-intelligence barge-in framework.

Chess danger coaching and tactical callouts should be implemented as a specialization of the existing barge-in preset/category model, not as a separate voice-routing rule.

## Why

The current runtime has:

- prompt/personality shaping for chess coaching behavior
- `BrainResultPriority` routing (`Interrupt`, `WhenIdle`, `Silent`)
- direct voice forwarding through `SendContextualUpdateAsync(...)`

The current runtime does **not** yet have:

- exchange-aware voice ownership
- reminder queue behavior
- category-based barge-in policy
- user-configurable allowed unsolicited speech classes

If chess danger warnings are implemented now as a special auto-speak path, they will bypass the product model already defined in `AUDIO_INTELLIGENCE_EXCHANGE_PROTOCOL.md` and create a second policy system that other agents will later have to unwind.

## Product Mapping

Chess-specific unsolicited speech belongs under the existing barge-in category:

- `CallOut`

This is the correct category for:

- king danger / check alerts
- mate threat warnings
- strong tactical opportunities
- immediate "do not miss this" coach interventions

It is **not**:

- `Reminder` unless the result arrives too late for direct surfacing
- `ToolExecution`
- `FreeCommentary`

## Current Recommended Behavior

Until the audio-intelligence barge-in runtime exists:

- allow chess coaching behavior to shape direct responses when the user explicitly asks
- allow prompt-level danger language to improve those responses
- do not add a new unsolicited auto-speech path that bypasses exchange/barge-in policy

This keeps the current system coherent while preserving the future architecture.

## Implementation Strategy

### Phase 1: Detection and classification seam

When the analysis layer can reliably determine king danger, expose structured metadata that voice routing can later consume.

Recommended fields:

- `king_safety_status`: `safe | pressured | check | near_mate`
- `danger_confidence`: numeric or normalized confidence bucket
- `escape_move`: optional best defensive move
- `mistake_summary`: optional short explanation of what caused the danger
- `threat_sequence`: optional move recap only when reliable
- `callout_category`: `CallOut`

This phase may land before barge-in speech is enabled, but it should remain non-speaking metadata until the runtime policy layer exists.

That metadata may later include shared semantic-class fields consumed by voice orchestration, for example:

- `primary_semantic_class`: `Danger | Opportunity | Assessment | SageAdvice | ImageAnalysis`
- `urgency`
- `speakability`
- `suggested_speech_mode`

Those fields belong to the broader audio-intelligence feature lane, not the immediate grounding bug-fix lane.

### Phase 2: Audio-intelligence integration

When the exchange/barge-in system is implemented:

- classify high-confidence chess danger outputs as `CallOut`
- respect exchange state first
- if exchange is inactive:
  - speak only when barge-in is enabled
  - speak only when `CallOut` is allowed
  - otherwise queue as reminder or absorb into context

### Phase 3: Chess-specific callout presets

Within the generic preset system, define chess-specific thresholds:

- `CheckDanger`
- `MateThreat`
- `WinningTactic`

These are chess semantics, not new top-level barge-in categories.

## Interrupt Rule

Chess may be the first agent family that meaningfully uses interruption, but it should still follow the shared model.

Recommended rule:

- use interrupt-style speech only for high-confidence `check` / `near_mate` style danger
- still map that event to `CallOut`
- do not treat "interrupt" as a separate user-facing category

In other words:

- `CallOut` is the product-facing policy bucket
- `Interrupt` is an internal delivery urgency only

## Coaching Behavior Relationship

The two chess coaching behaviors now defined in prompts should feed this system:

- advice for how to improve / win
- advice for how to survive danger

The danger pattern should become voice-callout eligible only when the runtime can prove:

- board-state confidence is high enough
- the event is timely
- exchange/barge-in policy allows it

## Manual Validation Once Implemented

When the barge-in slice exists, verify:

1. In a connected chess voice session with barge-in off:
   - danger stays silent unless asked
   - result becomes reminder or silent context
2. With barge-in on but `CallOut` disabled:
   - danger still does not speak
3. With barge-in on and `CallOut` enabled:
   - high-confidence check or mate danger can speak
4. Low-confidence danger:
   - should not hard-interrupt as fact
   - should use softer language or defer

## Recommendation

Do not implement chess auto-barge-in as a standalone feature now.

Instead:

- keep current coaching behavior prompt work
- add structured danger metadata when the analysis layer is ready
- implement spoken chess danger inside the audio-intelligence barge-in rotation
- treat chess as the first concrete specialization of the shared preset system, not an exception to it
