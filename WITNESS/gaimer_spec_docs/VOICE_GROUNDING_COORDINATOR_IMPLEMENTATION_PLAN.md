# Voice Grounding Coordinator Implementation Plan

**Project:** Gaimer Desktop (.NET MAUI)  
**Date:** 2026-03-19  
**Status:** Planned  
**Priority:** High next stabilization rotation after current accepted work closes  
**Scope:** Prevent voice-side board-state hallucination by introducing a grounded orchestration layer between realtime conversation and the factual brain pipeline

---

## Summary

Live testing indicates a real architectural gap:

- the **brain** path is image-driven, tool-capable, and relatively grounded
- the **voice** path is realtime and conversational, but not equivalently grounded

This produces a user-visible failure mode:

- voice confidently describes fake board states or hallucinated changes
- meanwhile MainView text output, driven by brain results, is materially more accurate

The correct fix is **not** to collapse brain and voice into one service.

The correct fix is to preserve the separation while adding a **Voice Grounding Coordinator** that gives voice an explicit factual contract:

1. classify the user turn
2. determine whether the turn requires grounded game-state knowledge
3. consult fresh brain-derived facts when needed
4. delay, defer, or express uncertainty when grounding is unavailable
5. keep voice fast for non-board-sensitive conversation

This plan defines that architecture.

---

## Rotation Boundary

This plan should be split across two different execution tiers:

- **Immediate stabilization rotation**
  - stop voice from making ungrounded board-state claims
  - gate board-sensitive replies on fresh grounded facts
  - add transcript and grounding telemetry needed for live validation
- **Later audio-intelligence feature work**
  - semantic-class-aware spoken delivery
  - reminder queue and exchange-aware carry-forward
  - richer barge-in policy and delayed speech orchestration

The next rotation should implement only the stabilization tier.
It should not absorb the broader audio-intelligence design in the same slice.

---

## Problem Statement

## Observed live behavior

The user reports:

- voice often knows that movement has happened, but not the true resulting board state
- voice invents piece locations or position claims
- text output in MainView remains comparatively accurate and tracks board changes better

That pattern strongly suggests the voice provider is speaking from:

- stale context
- weak provider-side reasoning
- conversational improvisation

rather than from the app's grounded brain output.

## Why this is happening in code

### 1. Voice does not receive raw images through the app pipeline

[MainViewModel.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs#L366) explicitly documents the rule:

- voice never receives raw images
- brain is the sole visual consumer

### 2. OpenAI Realtime cannot see the board at all

[OpenAIConversationProvider.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Conversation/Providers/OpenAIConversationProvider.cs#L53) sets:

- `SupportsVideo => false`

and [OpenAIConversationProvider.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Conversation/Providers/OpenAIConversationProvider.cs#L88) makes image send a no-op.

So any board-state claim from OpenAI Realtime is necessarily indirect.

### 3. Voice currently operates as a provider-driven conversational loop

The realtime provider emits:

- `TextReceived`
- `AudioReceived`
- `Interrupted`
- `ErrorOccurred`

See [IConversationProvider.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Conversation/IConversationProvider.cs#L32).

[MainViewModel.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs#L421) routes `TextReceived` directly into app chat/timeline/UI surfaces, but does **not** enforce any grounding gate for board-sensitive content.

### 4. Brain-to-voice grounding is just contextual text injection

[BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs#L505) forwards `VoiceNarration` to voice via `SendContextualUpdateAsync(...)`.

This is useful, but it is not a true orchestration layer. It does not guarantee:

- fresh board context
- turn classification
- tool completion before board claims
- explicit uncertainty when facts are stale

---

## Current Architecture Review

## Brain path

The brain path already has most of the right properties:

- image input
- structured prompt
- tool calls
- result channel
- routed timeline outputs
- confidence / structured analysis paths

Primary files:

- [OpenRouterBrainService.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Brain/OpenRouterBrainService.cs)
- [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs)

## Voice path

The voice path is optimized for responsiveness:

- websocket conversation provider
- low-latency audio send/receive
- direct provider text/audio flow
- interruption support

Primary files:

- [GeminiLiveService.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/GeminiLiveService.cs)
- [OpenAIRealtimeService.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/OpenAIRealtimeService.cs)
- [MainViewModel.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs)

## Core gap

There is currently no app-owned layer that answers:

1. Is this user turn board-sensitive?
2. Do we have fresh grounded brain facts?
3. Should voice answer immediately, defer, or express uncertainty?
4. Should voice trigger or await a fresh brain read before making factual claims?

That missing layer is the problem.

---

## Design Goal

Preserve the separation between brain and voice, but make the **brain the factual authority** for board-state claims.

Voice should remain responsible for:

- live cadence
- conversational flow
- tone
- pacing
- acknowledgements
- lightweight interaction

Brain should remain responsible for:

- factual board understanding
- tool-grounded claims
- structured assessment

The new Voice Grounding Coordinator should decide when voice is allowed to rely on brain facts, and when it must avoid certainty.

---

## Non-Goals

1. Merging voice and brain into one monolithic service.
2. Forcing voice to wait on brain for every single utterance.
3. Reintroducing raw image delivery to voice by default.
4. Building semantic-class-aware speech styling in this work.
5. Building the full exchange/reminder/barge-in runtime in this work.
6. Building the full generalist commentator system in this work.
7. Solving all future high-speed multimodal inference problems up front.

---

## Proposed Architecture

## 1. Add an explicit Voice Grounding Coordinator

Introduce a new seam, e.g.:

- `IVoiceGroundingCoordinator`

Responsibilities:

- receive voice-side user-turn context
- classify whether the turn needs grounded board facts
- consult a latest-known grounded fact cache
- decide response mode
- optionally trigger/await a fresh grounded brain refresh

Possible implementation:

- `VoiceGroundingCoordinator`

## 2. Add a grounded fact cache for voice use

Introduce a compact fact envelope produced from brain results, e.g.:

- `GroundedVoiceContext`

Suggested fields:

- `Summary`
- `PositionAssessment`
- `Threats`
- `SuggestedAction`
- `Fen`
- `Confidence`
- `Source`
- `CapturedAtUtc`
- `BrainResultAtUtc`
- `IsStale`
- `StalenessReason`

This should be app-owned structured data, not free-form provider memory.

## 3. Classify voice turns before allowing factual claims

Introduce turn classes, e.g.:

- `BoardSensitive`
- `GeneralGameQuestion`
- `Social`
- `Control`
- `Unclear`

Examples:

- "What's happening on the board?" -> `BoardSensitive`
- "Am I winning?" -> `BoardSensitive`
- "What do you think of this opening?" -> `GeneralGameQuestion`
- "Can you be quieter?" -> `Control`
- "That's crazy" -> `Social`

The classifier can start simple and deterministic.

## 4. Carry semantic class as metadata, not as immediate product scope

The brain can emit multiple distinct analysis types from one image pass:

- `ImageAnalysis`
- `Assessment`
- `Danger`
- `Opportunity`
- `SageAdvice`

The coordinator should be designed so it can later consume these semantic classes,
but the immediate stabilization slice only needs enough structure to preserve truthfulness.

Recommended near-term rule:

- voice may consume a **coarse grounded fact envelope** for truth gating
- voice does **not** yet need a full semantic-class-aware speech renderer

Recommended future-ready fields:

- `PrimarySemanticClass`
- `SecondarySemanticClasses`
- `Urgency`
- `Confidence`
- `Speakability`
- `SuggestedSpeechMode`

Those fields should be treated as forward-compatible extension points for the later audio-intelligence rotation, not blockers for the first grounding slice.

## 5. Immediate response modes

The first implementation only needs a few grounded response modes:

- `RespondFromGroundedContext`
- `AcknowledgeAndRefresh`
- `AcknowledgeUncertainty`
- `AnswerDirectlyWithoutBrain`
- `DeclineBoardCertainty`

This is enough to stop the current hallucination pattern without overfitting the future voice system.

## 6. Future semantic-class-aware speech model

Later audio-intelligence work should let voice adapt delivery based on semantic class without literally speaking the UI label.

Examples:

- `Danger` -> terse warning / possible interruption
- `Opportunity` -> quick tactical excitement
- `Assessment` -> calm positional framing
- `ImageAnalysis` -> observational grounding
- `SageAdvice` -> directive coaching tone

This should be expressed implicitly in voice style, not as literal phrases like "Sage advice:"

## 4. Add voice response modes

The coordinator should produce a response mode, e.g.:

- `Immediate`
- `GroundedImmediate`
- `GroundedAfterRefresh`
- `DeferredWithAcknowledgement`
- `Uncertain`

Meaning:

- `Immediate`: safe to answer without fresh board facts
- `GroundedImmediate`: answer now using recent grounded facts
- `GroundedAfterRefresh`: request or await fresh brain context first
- `DeferredWithAcknowledgement`: say "checking the board" or equivalent
- `Uncertain`: explicitly say facts are stale/unclear

## 5. Make provider capability explicit

Introduce policy state, e.g.:

- `VoiceGroundingMode.BrainOnly`
- `VoiceGroundingMode.ProviderVisionAllowed`
- `VoiceGroundingMode.Hybrid`

Recommended current default:

- `BrainOnly`

Because:

- OpenAI Realtime has no app-level vision path
- Gemini may support multimodal input in principle, but the app currently treats brain as the sole visual authority

## 6. Keep contextual updates, but make them structured

Current `SendContextualUpdateAsync(...)` text hints should eventually evolve toward structured grounding updates.

The coordinator should feed voice with:

- latest factual summary
- freshness state
- confidence
- allowed claim level

rather than only ad hoc narrative nudges.

---

## User-Facing Behavior Rules

## Safe default policy

### If the user asks a board-sensitive question

And fresh grounded context exists:

- answer from grounded facts
- use uncertainty language if confidence is low

And grounded context is stale:

- acknowledge
- indicate you are checking
- optionally trigger/await fresh brain evaluation

And grounded context is unavailable:

- do not invent the board state
- explain that you are not fully sure

### If the user asks a non-board-sensitive question

- answer immediately
- do not block on brain

### If the user asks during active heavy reasoning

- voice should remain responsive
- but factual claims should be bounded to:
  - latest grounded summary
  - explicit uncertainty
  - "checking now" defer behavior

---

## Recommended First Implementation Strategy

Start with the smallest viable grounding architecture.

## Phase A: Grounding contract, no provider rewrite

Deliver:

- `IVoiceGroundingCoordinator`
- `GroundedVoiceContext` model
- simple turn classifier
- staleness rules
- voice decision policy

Do not yet:

- rewrite realtime providers deeply
- add raw transcript persistence
- send images back into voice

## Phase B: Wire brain outputs into the fact cache

When brain results arrive, update the coordinator/cache with:

- latest grounded board summary
- timestamp
- confidence
- structured fields if available

Primary source points:

- [BrainEventRouter.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/BrainEventRouter.cs)
- possibly [OpenRouterBrainService.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/Services/Brain/OpenRouterBrainService.cs)

## Phase C: Gate board-sensitive voice responses

Before voice-side `TextReceived` is treated as trustworthy board commentary, route through coordinator logic.

Important principle:

- the coordinator should influence whether the app accepts provider claims as board-grounded
- not merely decorate hallucinations after the fact

## Phase D: Add telemetry for transcript and grounding behavior

This is essential before and during rollout.

---

## Telemetry Plan For This Work

Current telemetry is not enough to explain voice hallucination versus grounded behavior.

Add a dedicated follow-up telemetry slice for voice grounding.

Suggested events:

- `voice.turn.received`
- `voice.turn.classified`
- `voice.grounding_context.available`
- `voice.grounding_context.stale`
- `voice.grounding_context.missing`
- `voice.response_mode.selected`
- `voice.response.deferred`
- `voice.response.uncertain`
- `voice.input.transcript.final`
- `voice.output.transcript.final`
- `voice.response.interrupted`

Default payloads should be metadata only:

- provider
- turn_id
- classification
- staleness
- confidence bucket
- transcript length
- latency

Optional debug-gated payloads may include raw transcript text.

---

## Suggested Service Shape

## Interface

```csharp
public interface IVoiceGroundingCoordinator
{
    void UpdateGroundedContext(GroundedVoiceContext context);
    VoiceGroundingDecision ClassifyTurn(string userText, bool isVoiceTurn);
    Task<VoiceGroundingDecision> EvaluateAsync(
        string? userText,
        bool isVoiceTurn,
        CancellationToken ct = default);
}
```

## Decision model

```csharp
public sealed record VoiceGroundingDecision(
    VoiceTurnClass TurnClass,
    VoiceResponseMode ResponseMode,
    bool HasFreshGroundedContext,
    bool RequiresBrainRefresh,
    string? SafeGroundedSummary,
    string? Reason);
```

These are illustrative only. Final implementation should stay minimal.

---

## Where To Integrate

## Primary integration points

### 1. MainViewModel

Likely orchestration point for:

- user turn arrival
- provider `TextReceived`
- direct-message versus unsolicited routing

See [MainViewModel.cs](/Users/tonynlemadim/Developer/gAImer_desktop/src/WitnessDesktop/WitnessDesktop/ViewModels/MainViewModel.cs#L421).

### 2. BrainEventRouter

Good source for:

- latest grounded structured analysis
- voice-safe summaries
- freshness updates

### 3. Conversation providers

Do **not** start by embedding all grounding policy in provider implementations.

Keep providers relatively transport-focused:

- websocket send/receive
- state transitions
- audio/text events

The coordinator should sit above them.

---

## Tests

## Unit tests

Add tests for:

- turn classification
- staleness rules
- board-sensitive question with fresh context -> grounded answer allowed
- board-sensitive question with stale context -> defer/uncertain
- social/general turn -> immediate answer allowed

## Integration tests

Add tests proving:

- brain updates refresh voice grounding cache
- board-sensitive voice reply path is gated
- stale grounded context blocks false certainty
- OpenAI path in `BrainOnly` mode never acts as if it has direct vision

## Regression tests

Add scenarios for:

- user asks "what changed?" while fresh brain summary exists
- user asks "what piece is on e4?" with stale context
- user makes a non-game social comment during brain work

---

## Slice Plan

## Slice 1: Grounding seam + deterministic turn classification

Scope:

- add coordinator interface + implementation
- add `GroundedVoiceContext`
- implement simple deterministic turn classification
- implement staleness rules
- add unit tests

Acceptance:

- app has a real grounding policy seam
- classification and staleness rules are tested

## Slice 2: Brain-to-voice factual context cache

Scope:

- update coordinator from grounded brain outputs
- store latest fact envelope with timestamps/confidence
- add integration tests

Acceptance:

- latest grounded board summary is available to voice orchestration

## Slice 3: Voice response gating for board-sensitive turns

Scope:

- route board-sensitive voice turns through grounding decision
- add defer/uncertain behavior for stale facts
- avoid fake certainty

Acceptance:

- voice no longer makes unrestricted board-state claims when facts are stale or unavailable

## Slice 4: Voice grounding telemetry

Scope:

- add metadata-first transcript and grounding telemetry
- no raw transcript text by default
- debug-gated transcript text optional

Acceptance:

- next live test can explain whether voice drift came from transcript, stale facts, or provider behavior

## Slice 5: Optional provider-specific fast path

Scope:

- only if future provider capability warrants it
- explicit `ProviderVisionAllowed` / `Hybrid` mode

Acceptance:

- multimodal voice fast path is explicit and policy-controlled, not accidental

---

## Risks

1. Over-synchronizing voice to brain and making voice feel sluggish.
2. Over-engineering classification too early.
3. Letting provider-specific behavior leak into app policy.
4. Logging too much sensitive transcript content by default.

Recommended mitigation:

- start metadata-first
- keep deterministic rules before LLM-based orchestration
- keep brain as default factual authority

---

## Acceptance Criteria

This work should be considered successful when:

1. voice no longer confidently invents board state in stale/ungrounded situations
2. brain remains the factual source of truth for board-sensitive claims
3. voice still feels responsive for non-board-sensitive interaction
4. transcript/grounding telemetry exists to diagnose drift in future live tests
5. the design still permits future higher-speed multimodal voice paths without forcing them now

---

## Recommended Priority

This should be queued **after current live-test bug stabilization work**, but **before** major new feature work such as T17.

Reason:

- it is a direct live-test finding
- it affects trust in voice behavior
- it is architectural, not cosmetic
- it becomes easier to verify once current telemetry and brain hardening work are in place
