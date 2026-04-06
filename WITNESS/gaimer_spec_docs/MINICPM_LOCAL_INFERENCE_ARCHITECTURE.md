# MiniCPM Local Inference Architecture

**Project:** Gaimer V2
**Branch:** `MiniPCM`
**Date:** 2026-03-12
**Status:** Architecture baseline

---

## 1. Executive Summary

Gaimer V2 will migrate to a local-first inference architecture built around the MiniCPM-o family.

The correct architectural move is to preserve the `develop` application topology and swap providers behind existing seams. The app already has the right shape:
- `IBrainService` owns gameplay analysis
- `IConversationProvider` owns live voice interaction
- `BrainEventRouter` routes outputs to UI, voice, and ghost mode
- capture, timeline, ghost mode, and tooling are already application-owned

This makes the V2 effort a controlled provider migration rather than a platform rewrite.

---

## 2. Current Reference Architecture

Reference branch: `develop`

Current cloud-first topology:

```text
Screen Capture
  -> MainViewModel frame pipeline
  -> IBrainService (OpenRouterBrainService)
  -> Channel<BrainResult>
  -> BrainEventRouter
     -> Timeline
     -> Ghost Mode
     -> IConversationProvider contextual updates

User Mic
  -> IAudioService
  -> IConversationProvider (Gemini/OpenAI)
  -> AudioReceived / TextReceived
  -> MainViewModel / playback / UI
```

This topology remains valid for V2.

---

## 3. Target Architecture

### 3.1 Provider View

```text
IBrainService
  -> OpenRouterBrainService        (existing cloud)
  -> LocalMiniCpmBrainService      (new local)

IConversationProvider
  -> GeminiConversationProvider    (existing cloud)
  -> OpenAIConversationProvider    (existing cloud)
  -> LocalMiniCpmConversationProvider (new local)
```

### 3.2 Runtime View

```text
Gaimer App
  -> Local Runtime Adapter Layer
     -> Ollama Adapter                  (candidate)
     -> Direct Local Service Adapter    (future-safe)
  -> MiniCPM-o family runtime/model
```

The app must not couple its service layer directly to Ollama-specific assumptions.

---

## 4. Design Decisions

### D1. Preserve service seams

`IBrainService` and `IConversationProvider` remain the app-facing contracts.

Reason:
- the rest of the application is already stable against those interfaces
- this reduces blast radius and preserves testability

### D2. Brain migration leads voice migration

Local brain is the first production target.

Reason:
- screenshot analysis is the highest-value cloud dependency
- it fits the existing architecture cleanly
- local voice has higher runtime risk and should not block brain migration

### D3. Runtime adapter layer is mandatory

Introduce an internal local runtime abstraction for transport/process/runtime health concerns.

Reason:
- MiniCPM-o family support may vary by runtime
- we need freedom to change runtime strategy without changing app architecture

### D4. OpenRouter remains a supported fallback

Do not remove or rot the cloud path while building V2.

Reason:
- fallback is part of product reliability
- local runtime quality may vary by hardware and model version

### D5. Application tools remain outside the model runtime

Tool execution stays in C# services.

Reason:
- Stockfish, journals, capture, and future domain tools are product IP and deterministic assets
- the model should request tools, not own them

---

## 5. New Components

### 5.1 `ILocalModelRuntime`

Purpose:
- isolate process management, health checks, model readiness, and transport concerns from provider logic

Responsibilities:
- detect runtime availability
- validate model presence/version
- expose health status
- handle request/response boundaries
- surface structured runtime failures

This is an internal architecture seam, not a UI-facing interface.

### 5.2 `LocalMiniCpmBrainService`

Implements `IBrainService`.

Responsibilities:
- accept frame submissions from the current capture pipeline
- build MiniCPM-compatible multimodal prompts
- produce `BrainResult` through the existing channel pipeline
- preserve latest-frame-slot behavior and cancellation semantics from `OpenRouterBrainService`

Non-responsibilities:
- direct tool execution ownership
- UI logic
- fallback policy ownership

### 5.3 `LocalMiniCpmConversationProvider`

Implements `IConversationProvider`.

Responsibilities:
- manage local voice session lifecycle
- send user audio/text into local runtime
- emit audio/text/state events in the existing contract
- accept brain contextual updates in the same manner as cloud voice providers

Risk note:
- this component is allowed to ship after local brain if runtime quality is not sufficient

### 5.4 `InferenceProviderPolicy`

Purpose:
- centralize provider selection and fallback rules

Responsibilities:
- resolve effective provider mode from settings and environment
- decide whether session starts in local-only, local-first, or cloud-only mode
- determine failover behavior

This must not be scattered across `MauiProgram`, provider factories, and view models.

---

## 6. Provider Modes

Gaimer V2 will support these modes:

### Mode A: `cloud_only`
- brain: OpenRouter
- voice: existing cloud provider
- used for compatibility and fallback

### Mode B: `local_only`
- brain: LocalMiniCpmBrainService
- voice: local provider when enabled
- no silent cloud fallback

### Mode C: `local_first`
- preferred V2 mode
- local providers start first
- cloud fallback allowed by policy when local health checks fail

Provider mode must be surfaced in settings and diagnostics.

---

## 7. Voice Architecture Position

MiniCPM-o is the target local multimodal family, but voice must be treated as a staged reliability problem.

Therefore:
- the voice provider abstraction stays intact
- the local voice provider may internally use a staged stack before full unified multimodal interaction is production-ready
- the app must not assume that the same runtime path used for vision is automatically correct for low-latency duplex voice

This is the key anti-fragility decision for the branch.

---

## 8. Prompting and Context

### 8.1 What stays the same

- agent personalities
- game journal context
- L1/L2 context layering
- tool availability rules from session state
- illusion language rules and safety rails

### 8.2 What changes

- prompt formatting and transport must be adapted for MiniCPM-o runtime constraints
- structured outputs may need a local parser path independent from OpenRouter JSON schema features
- voice prompt/session configuration must be local-runtime-aware

---

## 9. Failure Model

The local stack must explicitly handle:
- runtime missing
- model missing
- model incompatible version
- runtime healthy but inference failing
- voice available while brain unavailable
- brain available while voice unavailable

Rules:
- partial local availability is allowed
- fallback must be visible
- no silent provider swaps

---

## 10. Telemetry and Diagnostics

The V2 branch must extend current telemetry with:
- selected provider mode
- local runtime health at session start
- model identity/version
- fallback cause
- average local frame latency
- local voice connect/start/stop failures

These events are required for live validation and debugging.

---

## 11. Implementation Constraints

1. Do not break `develop` capture semantics.
2. Do not bypass `BrainEventRouter`.
3. Do not move game tools into model prompts as fake capabilities.
4. Do not hard-code Ollama-specific assumptions into view models or UI.
5. Do not collapse provider selection into environment-variable sprawl.

---

## 12. Recommended First Slice

The first implementation slice should be:
- runtime abstraction
- local brain provider
- provider mode policy
- health checks
- explicit cloud fallback

Not:
- full local duplex voice
- installer UX
- Windows parity

This is the fastest path to a meaningful V2 milestone with limited regression risk.

---

## 13. Engineering Ownership Map

- PM/Architecture owner:
  - provider mode policy
  - service boundaries
  - phase sequencing
- Inference implementation owner:
  - runtime adapter
  - local brain provider
  - local voice provider
- Tester:
  - provider selection tests
  - fallback tests
  - session lifecycle tests
- Chronicler:
  - decisions
  - open threads
  - validation outcomes

---

## 14. Decision Summary

Gaimer V2 will use the MiniCPM-o family as the local-first inference direction, preserve `develop` as the application architecture, migrate the brain first, keep voice staged and explicit, and retain OpenRouter as a supported fallback path.
