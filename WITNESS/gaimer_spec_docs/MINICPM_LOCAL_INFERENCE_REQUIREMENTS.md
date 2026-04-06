# MiniCPM Local Inference Requirements

**Project:** Gaimer V2
**Branch:** `MiniPCM`
**Date:** 2026-03-12
**Owner:** Codex acting as project architecture owner
**Status:** Approved for planning

---

## 1. Objective

Move Gaimer from a cloud-first inference stack to a local-first inference stack using the MiniCPM-o family, while preserving the proven application architecture on `develop`.

This is a provider migration, not an application rewrite.

`develop` remains the architectural reference implementation:
- Screen capture remains unchanged
- BrainEventRouter remains the orchestration hub
- Timeline, ghost mode, and session model remain unchanged
- Local inference replaces cloud providers behind existing service seams

---

## 2. Product Outcome

Gaimer V2 must support:
- Local vision inference for captured gameplay frames
- Local voice interaction using the same agent personalities and conversation model
- Cloud fallback through OpenRouter when the local runtime is unavailable or user-disabled
- Runtime mode selection without re-architecting the UI or session pipeline

Gaimer V2 must not require cloud services for the core gameplay loop when local mode is enabled and healthy.

---

## 3. Primary Decision

The target model family for V2 local inference is **MiniCPM-o**.

Reasoning:
- It is the closest model family fit to Gaimer’s combined needs: image understanding, speech interaction, and local deployment.
- It aligns with the product vision already documented in the repo.
- It provides a realistic path to unifying vision and voice under one local multimodal direction.

Important scope constraint:
- We are committing to the **MiniCPM-o family**, not to a single immutable checkpoint version.
- We are not committing to Ollama as the only runtime boundary for all local features.

---

## 4. Requirements

### 4.1 Functional Requirements

1. The app must support a local brain provider that implements `IBrainService`.
2. The app must support a local voice provider that implements `IConversationProvider`.
3. Local brain inference must consume the same frame pipeline used on `develop`.
4. Local brain responses must continue to emit `BrainResult` objects through the existing channel pipeline.
5. Local voice must continue to emit `AudioReceived`, `TextReceived`, `ConnectionStateChanged`, and `ErrorOccurred` events through the existing provider interface.
6. Agent personality composition must remain intact across local and cloud providers.
7. Tool execution must remain local application logic, not delegated to the model runtime.
8. OpenRouter must remain available as a fallback/support provider.
9. The user must be able to choose local-first, cloud-first, or fallback mode from settings/config.
10. The app must surface when local inference is degraded, unavailable, or falling back.

### 4.2 Non-Functional Requirements

1. The migration must preserve the `develop` event flow and interaction model.
2. Local mode must degrade gracefully rather than silently failing.
3. Local mode must not block the capture pipeline.
4. Local mode must remain observable with structured telemetry and logs.
5. Model/runtime availability checks must happen before starting a session, not only after errors.
6. The first implementation wave must prioritize determinism and recoverability over absolute feature breadth.

### 4.3 UX Requirements

1. The user must understand whether Gaimer is running:
   - Local only
   - Cloud only
   - Local with cloud fallback
2. If local runtime/model assets are missing, the app must present a guided recovery path.
3. Local mode must preserve the core Gaimer illusion:
   - The agent is always watching
   - The agent speaks in-character
   - Ghost mode and timeline behavior are unchanged

---

## 5. Explicit Non-Goals

The first MiniCPM implementation phase will not:
- Replace the entire app with a custom local inference engine
- Rebuild timeline, ghost mode, or session orchestration
- Remove OpenRouter support
- Require fully unified local vision + duplex speech in a single runtime process on day one
- Solve persistence, analytics, or Windows parity in the same phase

---

## 6. Architecture Rules

1. **No architectural bypasses.**
   Local inference must enter and leave through the same service interfaces used today.

2. **Brain remains the sole consumer of visual data for gameplay analysis.**
   Do not reintroduce direct voice-from-image paths.

3. **Voice remains downstream of the brain for gameplay guidance.**
   Local voice may support user conversation, but gameplay interpretation still comes through brain outputs and routed context.

4. **Tools remain application-owned.**
   Stockfish, game journal, web search, capture utilities, and future helpers stay in C# services.

5. **Fallback is explicit.**
   If local runtime health fails, fallback must be visible and auditable.

---

## 7. Runtime Strategy

### 7.1 Local Brain

The first-class V2 goal is a local `IBrainService` implementation driven by MiniCPM-o.

This is the least ambiguous and highest-value migration target because:
- it maps cleanly onto existing frame submission and result channel patterns
- it removes the most expensive cloud dependency
- it keeps the rest of the app stable

### 7.2 Local Voice

Local voice is part of the V2 target, but it is not allowed to destabilize the first brain migration wave.

Therefore:
- local voice must be designed now
- local voice does not have to ship in the same implementation slice as local brain if runtime quality is not production-safe

### 7.3 Runtime Boundary

The application must support a local runtime adapter layer rather than binding the app directly to a single CLI or daemon protocol.

Implication:
- Ollama can be one adapter
- Direct local service hosting can be another
- The app-facing interfaces stay stable

---

## 8. Delivery Gates

Before implementation is considered complete, the following must be true:
- local brain provider is wired behind `IBrainService`
- provider selection is deterministic and test-covered
- fallback behavior is explicit and test-covered
- live session can run with local-first mode enabled
- ghost mode, timeline, and main dashboard work without architectural regressions

---

## 9. Open Decisions Reserved For Planning

These are intentionally deferred to the implementation planning pass:
- exact MiniCPM-o checkpoint/version to target first
- exact local runtime adapter set
- whether local voice ships as one provider or a staged provider stack
- how model assets are installed, updated, and health-checked
- whether fallback is automatic, opt-in, or user-configurable by feature

---

## 10. Acceptance Statement

This requirements document authorizes the team to plan and implement Gaimer V2 as a local-first MiniCPM-o migration that preserves `develop` as the architectural reference and keeps OpenRouter as fallback support.
