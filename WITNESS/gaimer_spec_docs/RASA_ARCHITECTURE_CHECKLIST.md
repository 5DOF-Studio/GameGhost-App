# Rasa Architecture Checklist

**Status:** Proposed
**Date:** March 16, 2026
**Purpose:** Concrete structural checklist for implementing `Rasa` in a way that fits the current Gaimer system without inheriting chess-specific assumptions.

## Goal

Implement `Rasa` as a game-agnostic commentator agent using the existing personality architecture, short-term context pipeline, and brain/voice split, while avoiding the structural mistakes exposed by the chess-agent path.

## Core Principle

`Rasa` should reuse the system's strengths:

- structured personality composition
- compact brain personality prefix
- rolling short-term context
- voice/brain separation
- explicit voice behavior design

`Rasa` should not inherit the system's current chess-specific defaults:

- chess-only prompt builder
- session-wide tool gating
- chess-shaped journal semantics
- board-specific language contracts
- under-specified voice behavior

## Agent Design Contract

Rasa should follow the same full agent-design contract that the chess agents now establish:

1. personality
2. brain behavior
3. tools
4. voice behavior

The key rule is:

- these are authored together
- they are not layered in one by one after implementation starts

Rasa should therefore have an explicit future `VOICE.md`, not just a generic STYLE section.

## Checklist

### 1. Generalize the live-perception contract

Requirement:

- `Rasa` needs a reusable live observation contract equivalent to the chess live-board contract, but game-agnostic.

Implementation target:

- Add a generalized contract concept on `Agent`, such as `LiveObservationLanguageContract`.
- This contract should be reusable by:
  - realtime voice setup
  - text chat
  - local voice backend
  - any future fallback prompts

Success criteria:

- `Rasa` never says it cannot see the game while connected unless the system explicitly knows context is stale or unavailable.
- `Rasa` never references screenshots, captures, or images as a user-facing framing device.

### 2. Split prompt builders by agent or domain

Requirement:

- `Rasa` must not use the current chess-specific `BrainPromptBuilder` as-is.

Implementation target:

- Introduce agent-selected or domain-selected brain prompt builders.
- Keep shared context assembly, but move:
  - capabilities
  - output contract
  - scene interpretation guidance
  - tool-calling policy
  into per-agent or per-domain builders.

Success criteria:

- `Rasa` gets a game-scene prompt, not a board/FEN prompt.
- Chess remains on its current specialized builder.

### 3. Make tool exposure agent-scoped

Requirement:

- `Rasa` should only see the tools that belong to `Rasa`.

Implementation target:

- Replace session-only tool gating with agent-aware capability resolution.
- Treat `SessionManager` as state holder, not final authority on tools.

Recommended structure:

- global tools
- agent-scoped tools
- optional domain tool packs

Success criteria:

- `Rasa` does not inherit chess tools.
- Chess agents retain chess tools.

### 4. Define a dedicated Rasa session knowledge store

Requirement:

- `Rasa` needs a generic session-state representation instead of reusing chess journal entries.

Implementation target:

- Add a `RasaSessionKnowledge` or similar service for:
  - game identity
  - genre
  - mode
  - player goal
  - confirmed facts
  - inferred facts
  - open questions
  - key moments
  - commentary preferences

Success criteria:

- `Rasa` can build understanding over a session without pretending to have durable memory.
- This state can later be linked to persistence and living-journal retrieval.

### 5. Keep rolling context as short-term memory, not long-term memory

Requirement:

- `BrainContextService` should remain the short-term, bounded working set.

Implementation target:

- Feed `Rasa` session knowledge into rolling context rather than replacing rolling context with persistence.
- Later persistent retrieval should enrich L1/L2, not bypass it.

Success criteria:

- `Rasa` short-term context remains prompt-budgeted and fresh.
- Persistent memory, when added later, becomes one source for conscious context rather than a parallel uncontrolled channel.

### 6. Add a voice-side rich-context pull path

Requirement:

- The design calls for voice to be able to request rich latest context on demand.

Implementation target:

- Add a dedicated runtime path such as:
  - `get_recent_context_for_voice`
  - `get_commentary_context`
  - `get_live_game_context`

That path should be able to combine:

- rolling context
- current session knowledge
- journal summary where relevant
- future retrieved persistent memory

Success criteria:

- Voice can pull context when the user asks for recap, clarification, or guidance.
- Routine updates can still use pushed context.

### 7. Add explicit response-pattern policies

Requirement:

- `Rasa` will need predictable commentary behavior, not just a personality blob.

Implementation target:

- Add structured response-pattern guidance for:
  - observation-first commentary
  - research-backed commentary
  - question-asking moments
  - recap behavior
  - quiet-mode behavior

Examples:

- observe -> react -> ask
- scene recap -> implication -> comment
- uncertainty -> clarify or research

Success criteria:

- `Rasa` commentary feels intentional rather than improvised from generic instructions.

### 8. Add question cadence and interruption policy

Requirement:

- `Rasa` should ask useful questions without becoming annoying.

Implementation target:

- Add explicit policy for:
  - downtime detection
  - maximum unresolved question count
  - cooldown between proactive questions
  - behavior when user says to stop asking

Success criteria:

- `Rasa` asks sparse, high-value questions.
- `Rasa` does not interrupt action-heavy moments with clarifications unless necessary.

### 9. Separate research tools from commentary generation

Requirement:

- `Rasa` needs research-backed understanding without blurring retrieval and narration.

Implementation target:

- Add source-aware research tools.
- Keep tool outputs structured and provenance-rich.
- Feed distilled facts into commentary, not raw research dumps.

Success criteria:

- `Rasa` can say useful game-specific things with source support.
- The commentary layer remains fast and human-sounding.

### 10. Add tests for non-chess negative space

Requirement:

- `Rasa` should be protected from accidental re-chessification.

Implementation target:

- Add tests ensuring:
  - `Rasa` prompts contain no FEN/chess-specific instructions
  - `Rasa` tool exposure excludes chess tools
  - live observation contract is present in voice/chat prompts
  - context retrieval is bounded and source-aware

Success criteria:

- Future refactors cannot quietly leak chess assumptions into `Rasa`.

## Recommended Delivery Order

### Phase 1

- generalized live observation contract
- agent-scoped capability resolution
- `Rasa`-specific prompt builder

### Phase 2

- `Rasa` session knowledge store
- explicit response pattern rules
- question cadence policy

### Phase 3

- voice rich-context pull path
- source-aware research tools

### Phase 4

- persistence bridge
- living journal retrieval
- context enrichment from durable memory

## Optional Salvage Resource: Jiga-Service Review

Status:

- non-core
- optimization/reference only
- do not treat as implementation authority

Purpose:

- Preserve reusable lessons from the failed `Jiga-Service` architecture without importing its complexity, config sprawl, or broken cohesion into `Rasa`.

Source repo:

- https://github.com/IkeGister/Jiga-Service

Use rule:

- Reuse ideas and seams, not the old architecture wholesale.
- Prefer extracting one local truth at a time instead of porting subsystems.

### Worth Revisiting Later

- `Docs/Skills_Spec.md`
  - Local idea worth keeping: perception capabilities should be separate from personality.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/Docs/Skills_Spec.md

- `src/Jiga/skills/generalGamingAssistance/general_gaming_coordinator.py`
  - Local idea worth keeping: heavy/stable detections can be cached while lightweight observation runs continuously.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/src/Jiga/skills/generalGamingAssistance/general_gaming_coordinator.py

- `src/Jiga/Intelligence/context/shared_context.py`
  - Local idea worth keeping: a normalized session-scoped short-term context seam between perception and chat/voice is valuable.
  - Warning: reuse the seam, not the concrete conversion-heavy implementation.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/src/Jiga/Intelligence/context/shared_context.py

- `src/Jiga/voice_tools/commentary_engine.py`
  - Local idea worth keeping: commentary arbitration rules such as cooldowns, urgency override, and suppressing speech over user speech.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/src/Jiga/voice_tools/commentary_engine.py

- `Docs/Docs/ChatBrainV2%20Design.md`
  - Local idea worth keeping: explicit speech protocol priority and interruption ordering.
  - Warning: do not revive the full ChatBrainV2 abstraction stack.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/Docs/Docs/ChatBrainV2%20Design.md

- `src/Jiga/Intelligence/dialogue_intent_module.py`
  - Local idea worth keeping: a small deterministic pre-router for obvious commands before hitting the expensive brain path.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/src/Jiga/Intelligence/dialogue_intent_module.py

- `Docs/AGENT_SPECIFICATION.md`
  - Local idea worth keeping: separating personality, voice style, and interaction defaults as authored surfaces.
  - Warning: do not recreate the marketplace/tier/config explosion from this document.
  - Repo URL: https://github.com/IkeGister/Jiga-Service/blob/main/Docs/AGENT_SPECIFICATION.md

### Explicitly Do Not Reuse

- user-facing skill-toggle matrices as a primary product model
- tier-heavy or marketplace-heavy agent schemas
- “universal cross-game” abstraction layers that are broader than current product evidence supports
- parallel subsystem stacks that all try to own voice, memory, context, and delivery at once

### Concrete Lessons For Rasa

- Keep personality separate from perception capability.
- Give `Rasa` a compact normalized session context instead of many raw skill outputs.
- Add commentary arbitration as a dedicated runtime policy layer.
- Use explicit speech-priority rules for interruptions and urgent callouts.
- Keep any deterministic intent front-door tiny and high-confidence.

### Constraint

- This section is a future optimization/reference resource only.
- It must not expand the minimum safe starting point for `Rasa`.
- If a future optimization from `Jiga-Service` conflicts with the simpler `Rasa` path, prefer simplicity.

## Minimum Safe Starting Point

Before implementing `Rasa`, the minimum structural bar should be:

- agent-scoped tools
- non-chess prompt path
- generalized live observation contract
- bounded generic session knowledge store

If those four are in place, `Rasa` can be added without fighting the system.
