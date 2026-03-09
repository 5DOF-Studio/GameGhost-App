# Next Sprint: Skills + Subagent Matrix

**Project:** Gaimer Desktop  
**Scope:** Chat V2 MVP + Brain Context Pipeline (Sprint 1 + Sprint 2)  
**Created:** February 20, 2026  
**Status:** Implementation-ready

---

## ONE-PAGE SUMMARY — First 5 Actions

| # | Action | Owner | Trigger |
|---|--------|-------|---------|
| 1 | **Freeze DTO contracts** — ReelMoment, SharedContextEnvelope, ChatMessageEvent per GMR-001 | Tech Lead + PM | PM mode: plan GMR-001 and assign specialists |
| 2 | **Add SendTextAsync to IConversationProvider** — implement in OpenAI, Mock, Gemini; wire MainViewModel | Realtime/Infra | Frontend engineer pass: implement SendTextAsync per GMR-003 |
| 3 | **Implement VisualReelService MVP** — append + rolling retention (GMR-006) | Capture | gsd-executor: implement VisualReelService per BRAIN_CONTEXT_PIPELINE_SPEC |
| 4 | **Add delivery state (Pending/Sent/Failed)** — ChatMessage.DeliveryState, UI metadata | Frontend + Realtime | Frontend engineer pass: implement delivery state UI per GMR-004 |
| 5 | **Chronicler: Update PROGRESS_LOG + FEATURE_LIST** — align with ticket board and spec docs | Chronicler | Chronicler pass: update progress/feature/spec docs for Chat V2 scope |

**Next:** Execute GMR-002 (typed chat events) → GMR-005 (error surfacing) → GMR-009 (SharedContextEnvelope builder).

---

## 1. Specialist Roles for Next Sprint

| Role | Purpose | Primary Subagent Mapping |
|------|---------|--------------------------|
| **PM Orchestrator** | Roadmap, ticket assignment, acceptance, handoff gate | `gsd-planner`, `gsd-plan-checker`, `gsd-integration-checker` |
| **Frontend Engineer** | UI/UX implementation, chat states, delivery feedback, MainView/MinimalView | `gsd-executor`, `explore` |
| **Realtime/Infra Specialist** | IConversationProvider, typed events, delivery state, error handling | `gsd-executor`, `generalPurpose` |
| **Capture Specialist** | VisualReelService, capture modes, reel query API | `gsd-executor`, `explore` |
| **LLM Context Specialist** | SharedContextEnvelope, token budgeting, L1/L2/L3 memory | `gsd-executor`, `generalPurpose` |
| **Chronicler** | Decision/progress logs, spec alignment, handoff docs | `generalPurpose`, `gsd-codebase-mapper` |
| **Showcaser** | README, release notes, PR quality | `generalPurpose` |

**Tech Lead** is not a separate subagent role; PM Orchestrator handles coordination. GMR-001 and GMR-015 are cross-role and driven by PM with specialist input.

---

## 2. Skills by Role (Sprint 1 / Sprint 2 / Nice-to-Have)

### PM Orchestrator

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| Backlog prioritization (P0/P1/P2) | Same + integration verification | Roadmap alignment with long-term phases |
| Dependency tracking across GMR-* | Cross-phase handoff verification | Risk register maintenance |
| Acceptance criteria definition | GMR-015 acceptance run orchestration | Stakeholder sync patterns |
| Ticket assignment by specialist | Mid-sprint scope adjustment | Velocity tracking |
| PLAN.md creation for waves | Gap closure planning | Sprint retrospective notes |

### Frontend Engineer

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| .NET MAUI / XAML layouts | Offline UX (disabled send, hint) | Design-system consistency with Figma |
| Chat bubble states (Pending/Sent/Failed) | Retry UI affordances on failed messages | Animation polish |
| MainView/MinimalView chat integration | Retention cap UI (if surfaced) | Accessibility (screen readers, contrast) |
| Delivery state visibility (opacity, icon, label) | Typed message styling (Warning, Lore) | Responsive layout edge cases |
| System/error message styling | Long-session UX under retention | Dark/light theme parity |

### Realtime/Infra Specialist

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| IConversationProvider extension (SendTextAsync) | Offline-aware send guard (IsConnected) | Observability (latency, error rates) |
| Typed MessageReceived event (ChatMessageEvent) | Retry strategy (auto + manual) | Provider health checks |
| Legacy TextReceived compatibility | Chat retention cap (in-memory) | Structured error codes |
| Delivery state transitions (Pending→Sent/Failed) | Debounce/throttle ErrorOccurred | WebSocket reconnection logic |
| ErrorOccurred → System message in chat | Send button disabled when disconnected | Provider capability discovery |

### Capture Specialist

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| ReelMoment, CaptureFrameEvent DTOs | Reel Query API (last N seconds, timestamp-adjacent) | Semantic retrieval index |
| VisualReelService MVP (append + retention) | Unit tests for timestamp boundaries | Performance tuning (large reels) |
| Bounded queue + drop policy | Integration with context envelope | Frame deduplication |
| baseline vs high-attention capture modes | — | Multi-target reel segmentation |

### LLM Context Specialist

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| SharedContextEnvelope builder | Session summarization loop (L3) | Confidence scoring refinement |
| Token budgeting (900 voice, 1200 chat, 1600 max) | Critical push alerts + debounce | Metrics/dashboards |
| L1 event store + L2 rolling summarizer | Richer confidence metadata | Multi-intent routing |
| GetContextForChatAsync with budget enforcement | Metrics (brain_context_build_latency_ms) | L3 incremental vs regenerate |
| Deterministic truncation priority | — | Alert category thresholds by game mode |

### Chronicler

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| PROGRESS_LOG.md updates per phase | Same + handoff docs for GMR-015 | Decision logs (ADRs) |
| FEATURE_LIST.md spec divergence tracking | BRAIN_CONTEXT_PIPELINE_SPEC alignment | Cross-spec traceability matrix |
| BUG_FIX_LOG.md per fix | CHAT_V2_EXECUTION_TICKET_BOARD status | API change log |
| Spec doc alignment (design → implementation) | Implementation notes for brain module | Release notes draft |

### Showcaser

| Sprint 1 | Sprint 2 | Nice-to-Have |
|----------|----------|--------------|
| README accuracy (build, run, scope) | Release notes when build stable | PR template with test plan |
| (Light load—MVP in progress) | Changelog prep for Chat V2 | Screenshot refresh |
| — | PR summary quality check | Demo script/documentation |

---

## 3. Role → Subagent Type Mapping

| Role | Primary Subagent | Fallback | Execution Pattern |
|------|------------------|----------|--------------------|
| PM Orchestrator | `gsd-planner` | `gsd-plan-checker`, `gsd-integration-checker` | Plan phases → assign tickets → verify integration |
| Frontend Engineer | `gsd-executor` | `explore` | Implement UI per spec → acquire patterns if needed |
| Realtime/Infra Specialist | `gsd-executor` | `generalPurpose` | Execute provider/event tasks atomically |
| Capture Specialist | `gsd-executor` | `explore` | Implement services per BRAIN_CONTEXT_PIPELINE_SPEC |
| LLM Context Specialist | `gsd-executor` | `generalPurpose` | Implement context pipeline per spec |
| Chronicler | `generalPurpose` | `gsd-codebase-mapper` | Doc updates after merges/milestones |
| Showcaser | `generalPurpose` | — | Release-facing docs when stable |

**Invocation patterns (from CURSOR_SUBAGENT_ROSTER.md):**

- **PM:** "PM mode: plan phase X and assign specialists."
- **Frontend Engineer:** "Frontend engineer pass: implement UI for X and acquire needed patterns."
- **Chronicler:** "Chronicler pass: update progress/feature/spec docs for X."
- **Showcaser:** "Showcaser pass: prepare repo/PR/release artifacts for X."

**Domain specialists (Realtime, Capture, LLM Context):** Use explicit task prompts, e.g.  
"Execute GMR-003: Add SendTextAsync to IConversationProvider per CHAT_V2_EXECUTION_TICKET_BOARD.md."

---

## 4. P0 Ticket → Role Ownership

| Ticket | Title | Owner | Subagent |
|--------|-------|-------|----------|
| **GMR-001** | Define Core Cross-Track Contracts | Tech Lead + PM (all specialists review) | PM + generalPurpose |
| **GMR-002** | Add Typed Chat Event Pipeline | Realtime/Infra | gsd-executor |
| **GMR-003** | Add SendTextAsync to Provider Contract | Realtime/Infra | gsd-executor |
| **GMR-004** | Chat Delivery State (Pending/Sent/Failed) | Frontend + Realtime | gsd-executor |
| **GMR-005** | In-Thread System Error Messaging | Realtime/Infra | gsd-executor |
| **GMR-006** | VisualReelService MVP | Capture | gsd-executor |
| **GMR-007** | Capture Modes and Bounded Queue | Capture | gsd-executor |
| **GMR-009** | SharedContextEnvelope Builder | LLM Context | gsd-executor |
| **GMR-010** | Token Budgeting and Truncation Rules | LLM Context | gsd-executor |
| **GMR-015** | Integration Scenario + Acceptance Run | PM + all specialists | gsd-integration-checker |

**Sprint 1 wave (parallelizable where no dependency):**

- **Wave 1:** GMR-001 (blocker for others)
- **Wave 2a:** GMR-002, GMR-003 (Realtime); GMR-006, GMR-007 (Capture); GMR-009, GMR-010 (LLM Context)
- **Wave 2b:** GMR-004, GMR-005 (after GMR-002, GMR-003)
- **Wave 3:** GMR-015 (initial run)

---

## 5. Assignment Matrix

| Specialist | Sprint 1 Tickets | Sprint 2 Tickets | Cross-Cut |
|------------|-----------------|------------------|-----------|
| **PM** | GMR-001, GMR-015 | GMR-015 (final) | Planning, acceptance |
| **Frontend** | GMR-004 (delivery state UI) | GMR-012 (offline UX) | MainView binding |
| **Realtime/Infra** | GMR-002, GMR-003, GMR-004 (state logic), GMR-005 | GMR-012, GMR-013, GMR-014 | Provider contract |
| **Capture** | GMR-006, GMR-007 | GMR-008 | Reel DTOs |
| **LLM Context** | GMR-009, GMR-010 | GMR-011 | Envelope contract |
| **Chronicler** | All doc updates | All doc updates | Spec alignment |
| **Showcaser** | Light | Release prep | — |

---

## 6. Handoff Protocol

### Standard Handoff Contract (per CURSOR_SUBAGENT_ROSTER.md)

Every specialist output must include:

1. **What changed** — Files touched, behavior added
2. **Why it changed** — Ticket/spec reference
3. **What remains** — Open items, follow-ups
4. **Risks/assumptions** — Caveats, platform notes
5. **Validation steps** — How to verify

### Cross-Role Handoffs

| From | To | Handoff Artifact |
|------|-----|------------------|
| Realtime | Frontend | Updated IConversationProvider, event signatures; Frontend wires MainView |
| Capture | LLM Context | ReelMoment DTO, VisualReelService API; LLM Context consumes for envelope |
| LLM Context | Realtime | SharedContextEnvelope shape; Realtime passes to chat request path |
| PM | All | PLAN.md, ticket assignments, acceptance criteria |

### Checkpoint Gates

- **After GMR-001:** All specialists confirm DTO contracts before implementation.
- **After Sprint 1 Wave 2:** Integration check — contracts aligned, no breaking changes.
- **GMR-015:** Human-verify end-to-end scenario before closing Sprint 1.

---

## 7. References

| Document | Purpose |
|----------|---------|
| `GAIMER/GameGhost/CURSOR_SUBAGENT_ROSTER.md` | Role definitions, invocation protocol |
| `GAIMER/GameGhost/gaimer_spec_docs/CHAT_V2_EXECUTION_TICKET_BOARD.md` | Ticket backlog, sprint breakdown |
| `GAIMER/GameGhost/gaimer_spec_docs/BRAIN_CONTEXT_PIPELINE_SPEC.md` | L1/L2/L3, DTOs, token budget |
| `GAIMER/GameGhost/gaimer_spec_docs/CHAT_V2_MVP_DELIVERABLE.md` | MVP definition, user stories, phases |
| `GAIMER/GameGhost/AGENT_HANDOFF_INSTRUCTIONS.md` | Build, test, project structure |
