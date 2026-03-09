# Phase 02: Chat Brain Architecture

**Status:** Not Started  
**Target:** Sprint 1-2 (2-3 weeks)  
**Source Spec:** `~/Desktop/agentic office/RemoteAgents/Gaimer-Desktop-Brain/chat-brain-design.md`  
**Companion Specs:**
- `~/Desktop/agentic office/RemoteAgents/Gaimer-Dross-ElevenLabs/brain-and-tools-reference.md`
- `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_CONTEXT_PIPELINE_SPEC.md`

---

## Overview

Implement the Chat Brain architecture for Gaimer Desktop, providing a unified event routing system, timeline-based chat feed, session state machine, and proactive Brain alerts. This phase transforms the flat chat list into a hierarchical timeline feed with checkpoints, event grouping, and distinct rendering for Brain-initiated (proactive) messages.

---

## Goals

1. **Unified Event Routing** — All Brain output flows through `BrainEventRouter` to Timeline, Voice, and Top Strip consumers
2. **Timeline Feed Model** — Hierarchical structure: Checkpoints → EventLines → Events (replaces flat chat list)
3. **Session State Machine** — OutGame/InGame states with tool availability gating
4. **Proactive Rendering** — Brain-initiated alerts render distinctly from user/assistant messages
5. **Dynamic Prompt Assembly** — Chat system prompt adapts to session context and available tools

---

## Plans

| # | Plan | Description | Est. Effort |
|---|------|-------------|-------------|
| 01 | Core Models | Session context, message models, timeline structures, **generic vs agent-specific event types**, **agent availability gating** | 5-7 hrs |
| 02 | Session State Machine | OutGame/InGame transitions, tool availability gating | 2-3 hrs |
| 03 | Brain Event Router | Central hub routing Brain output to all consumers | 4-6 hrs |
| 04 | Timeline Feed Manager | Checkpoint creation, event stacking, feed management | 4-6 hrs |
| 05 | Chat Prompt Builder | Dynamic system prompt assembly with session context | 3-4 hrs |
| 06 | Timeline UI Component | XAML implementation of timeline pattern | 8-12 hrs |

**Total Estimated Effort:** 26-38 hours

---

## Agent Availability

Initially, only **Leroy (Chess)** agent is available. Other agents are gated until their event types, tools, and icons are fully designed:

| Agent | Type | Status | Reason |
|-------|------|--------|--------|
| Leroy | Chess | ✅ Available | Events + icons ready |
| Derek | RPG | ❌ Gated | RPG events/tools not designed |
| Wasp | FPS | ❌ Gated | FPS events/tools not designed |

---

## Event Type Architecture

Events are separated into **Generic** (reusable) and **Agent-Specific**:

### Generic Events (All Agents)
- Danger, Opportunity, BestMove, SageAdvice, Assessment
- GameStateChange, DirectMessage, ImageAnalysis
- AnalyticsResult, HistoryRecall, GeneralChat

### Chess Events (Leroy)
- PositionEval, Tactic, Opening, Endgame, TimeControl

### FPS Events (Wasp) — Future
- ThreatDetected, CoverAdvice, LoadoutTip, ObjectiveUpdate

### RPG Events (Derek) — Future
- QuestHint, LoreNote, BuildAdvice, ItemAlert

---

## Dependencies

### Requires (from existing implementation):
- `IConversationProvider` abstraction (voice/chat providers)
- `IBrainContextService` (context envelope assembly)
- `IVisualReelService` (captured frames)
- `MainViewModel` (existing bindings)

### Provides (for future phases):
- `BrainEventRouter` for unified event distribution
- `TimelineFeed` service for checkpoint-based chat history
- `SessionContext` for state-aware tool selection
- Timeline UI component for rich chat rendering

---

## Key Architectural Decisions

### From Design Spec:

1. **Two Output Channels** — Voice (ElevenLabs) and Text Chat (direct LLM API) share the same Brain pipeline but consume independently

2. **Proactive as Distinct Role** — `MessageRole.Proactive` renders differently from `Assistant` (signal badge, urgency accent color)

3. **Checkpoint-Based Timeline** — New capture = new checkpoint header; events stack within checkpoints by type

4. **Intent via Tool-Calling** — No separate classifier; LLM tool-calling behavior determines intent

5. **Session-Gated Tools** — InGame enables capture/analysis tools; OutGame restricts to history/analytics

---

## Success Criteria

- [ ] Brain events route to Timeline, Voice, and Top Strip simultaneously
- [ ] New screen capture creates new checkpoint in timeline
- [ ] Events of same type stack horizontally within checkpoints
- [ ] Proactive messages render with signal badge and urgency accent
- [ ] Direct messages appear as bubbles within their checkpoint
- [ ] Tool availability changes based on SessionState
- [ ] System prompt includes session context dynamically
- [ ] Auto-scroll to latest checkpoint on new capture

---

## UI Deliverables

The Timeline UI follows the "agent-plan component" pattern:

```
📷 Capture #7 — 3:12 in [in-game]
  ┊ 🖼 White pawn to e4, Italian Game setup
  ┊ ⚡ Fork available — knight to c6

📷 Capture #8 — 3:18 in [in-game]
  ┊ ⚠ Blunder! Queen exposed after that pawn push
  ┊ 💬 "what happened?"
  ┊    → "Your opponent pushed d5 and left their queen hanging..."
```

### Icon Reference

| EventOutputType | Icon | Context |
|-----------------|------|---------|
| Tactic | ⚔️ | Fork, pin, skewer |
| PositionEval | 📊 | Evaluation change |
| BestMove | 🎯 | Suggested action |
| Danger | ⚠️ | Blunder, hanging piece |
| Opportunity | ⚡ | Winning tactic |
| GameStateChange | 🏁 | Check, checkmate, phase |
| DirectMessage | 💬 | User + Brain exchange |
| ImageAnalysis | 🖼️ | Screen analysis text |
| AnalyticsResult | 📈 | Stats, win rate |
| HistoryRecall | 📋 | Past game reference |
| GeneralChat | 💭 | Casual conversation |

---

## Execution Order

```
01-core-models
      │
      ├──► 02-session-state-machine
      │           │
      │           └──► 05-chat-prompt-builder
      │
      ├──► 03-brain-event-router
      │           │
      │           └──► 04-timeline-feed-manager
      │                         │
      │                         └──► 06-timeline-ui
      │
      └──► (parallel track possible)
```

Plans 01-05 can be executed in ~2 parallel tracks. Plan 06 (UI) requires 04 complete.

---

## Out of Scope (Phase 3+)

- Persistence layer (SQLite chat history)
- Session replay / history browser
- Multi-game connector support
- Screenshot gallery / capture browser
- Timeline animations (equivalent to framer-motion)
