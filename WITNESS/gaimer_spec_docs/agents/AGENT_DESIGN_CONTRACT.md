# Agent Design Contract

**Status:** Active  
**Date:** 2026-03-19  
**Purpose:** Define the required design surfaces for every Gaimer agent so personality, brain behavior, tools, and voice behavior are authored together rather than drifting apart later.

---

## Principle

An agent is not just a personality blob.

Every agent in Gaimer should be designed as a complete operational package with four first-class parts:

1. **Personality**
2. **Brain Behavior**
3. **Tools**
4. **Voice Behavior**

These should be authored together for the games that agent supports.

The goal is to avoid a common failure mode:

- good personality writing
- decent brain prompt
- tools bolted on later
- voice left under-specified or generic

That drift makes the agent feel inconsistent across text, analysis, and speech.

---

## Required Design Surfaces

## 1. Personality

Purpose:

- define who the agent is
- define how the agent sounds
- define behavioral rules and situational modes

Required files:

- `SOUL.md`
- `STYLE.md`
- `BEHAVIOR.md`
- `SITUATIONS.md`
- `ANTI_PATTERNS.md`
- `EXAMPLES.md`

These define the identity and interaction shape of the agent across all surfaces.

## 2. Brain Behavior

Purpose:

- define how the agent reasons
- define what the brain path is allowed to assert
- define the structured output and grounding rules for the supported games

This should include:

- supported game/domain assumptions
- observation contract
- uncertainty rules
- structured output requirements
- freshness / grounding requirements
- game-specific reasoning patterns

This is not just "personality in the brain." It is the operational analysis contract.

## 3. Tools

Purpose:

- define what the agent can use to ground or extend its knowledge
- ensure tools are scoped to the games and behaviors the agent supports

This should include:

- minimum tool surface
- tool usage policy
- when tools are required versus optional
- how tool outputs should influence commentary

Tool access must be agent-aware, not merely session-aware.

## 4. Voice Behavior

Purpose:

- define how the agent behaves in live speech
- define how voice differs from text when needed
- define grounding, cadence, interruption, and uncertainty behavior

Required file:

- `VOICE.md`

This should include:

- speaking cadence
- acknowledgement patterns
- interruption policy
- when to stay silent
- what voice can say without fresh grounded facts
- how voice should behave during uncertainty
- speech-specific phrasing rules
- game-specific spoken-language conventions

Voice behavior is not optional. It is a primary part of the agent.

---

## Required Questions For Every New Agent

Before an agent is considered ready for implementation, the design should answer:

1. Who is this agent?
2. How does this agent sound in text?
3. How does this agent reason about its supported games?
4. What tools is this agent allowed to use?
5. How does this agent speak in realtime voice?
6. What can this agent say confidently?
7. What must this agent avoid claiming without grounding?
8. How should this agent behave when context is stale or uncertain?
9. What are the supported games and what changes between them?
10. How do brain behavior and voice behavior stay aligned instead of drifting?

---

## Implementation Mapping

The design surfaces should map into runtime roughly like this:

- Personality -> `ComposedPersonality`
- Brain Behavior -> `BrainPersonalityPrefix`, prompt builders, grounding rules, structured output contracts
- Tools -> agent-scoped tool availability and tool guidance
- Voice Behavior -> realtime conversation policy, grounding coordinator rules, cadence/interruption handling, speech conventions

The exact code seams may evolve, but the design contract should remain stable.

---

## Minimum Deliverable For A New Agent

No new agent should be considered structurally ready until it has:

- personality files
- a brain behavior plan
- a tool surface definition
- a voice behavior specification
- supported-game scope clearly stated

This applies to:

- Rasa
- future RPG agents
- future FPS agents
- any specialist or generalist variants

---

## Current Chess Baseline

The chess agents should be treated as the first complete template.

Baseline artifacts:

- Leroy personality set
- Wasp personality set
- dedicated chess brain behavior already embedded in prompts and tools
- dedicated chess tool surface
- explicit chess voice behavior docs added as part of this design contract

Future agents should follow this pattern instead of inventing their own incomplete structure.
