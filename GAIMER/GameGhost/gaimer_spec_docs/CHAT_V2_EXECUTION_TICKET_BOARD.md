# Chat V2 Execution Ticket Board

**Project:** Gaimer Desktop  
**Scope:** Reels capture mechanism, shared LLM context brain, and chat infra integration  
**Sprint Horizon:** Sprint 1 (1 week) + Sprint 2 (1-2 weeks)  
**Owners:** Capture Specialist, LLM Context Specialist, Realtime/Infra Specialist

---

## Companion Specs

- `BRAIN_CONTEXT_PIPELINE_SPEC.md` - full design and implementation spec for pull-first context envelopes, three-layer memory model (L1/L2/L3), token budgeting, and critical push alerts.

---

## Priority Legend

- `P0` Required for MVP
- `P1` Hardening and reliability
- `P2` Post-MVP enhancements

---

## Ticket Backlog (15 Tickets)

### GMR-001 - Define Core Cross-Track Contracts
- **Priority:** P0
- **Owner:** Tech Lead + all specialists
- **Description:** Freeze DTO contracts for reel events, shared context envelope, and typed chat events.
- **Acceptance Criteria:**
  - `ReelMoment`, `ReelQuery`, `CaptureFrameEvent` contract documented.
  - `SharedContextEnvelope` contract documented with max token budget fields.
  - `ChatMessageEvent` contract includes `User`, `AiInsight`, `System` at minimum.
  - Reviewed and approved by all three tracks.
- **Dependencies:** None

### GMR-002 - Add Typed Chat Event Pipeline
- **Priority:** P0
- **Owner:** Realtime/Infra Specialist
- **Description:** Extend provider abstraction to emit typed message events in addition to plain text.
- **Acceptance Criteria:**
  - `IConversationProvider` exposes typed `MessageReceived` event.
  - Legacy `TextReceived` remains for backward compatibility.
  - MainView subscribes to typed events without breaking existing flow.
- **Dependencies:** GMR-001

### GMR-003 - Add SendTextAsync to Provider Contract
- **Priority:** P0
- **Owner:** Realtime/Infra Specialist
- **Description:** Add outbound text send support to provider interface and implementations.
- **Acceptance Criteria:**
  - `SendTextAsync` added to abstraction and implemented in active providers.
  - `SendTextMessageCommand` calls provider send path.
  - Input is disabled or guarded when provider is disconnected.
- **Dependencies:** GMR-001

### GMR-004 - Chat Delivery State (Pending/Sent/Failed)
- **Priority:** P0
- **Owner:** Realtime/Infra Specialist
- **Description:** Add per-message delivery state for user messages.
- **Acceptance Criteria:**
  - User message appears as `Pending` immediately.
  - State transitions to `Sent` on provider success.
  - State transitions to `Failed` on timeout/error.
  - UI state is visible in chat bubble metadata.
- **Dependencies:** GMR-003

### GMR-005 - In-Thread System Error Messaging
- **Priority:** P0
- **Owner:** Realtime/Infra Specialist
- **Description:** Surface provider/network errors as system chat entries.
- **Acceptance Criteria:**
  - Error callbacks create `System` messages in chat feed.
  - Duplicate rapid errors are throttled/debounced.
  - No critical error is only console-log visible.
- **Dependencies:** GMR-002

### GMR-006 - VisualReelService MVP (Append + Rolling Retention)
- **Priority:** P0
- **Owner:** Capture Specialist
- **Description:** Build local reel storage service for captured frames with retention policy.
- **Acceptance Criteria:**
  - Supports append with timestamp + source metadata.
  - Rolling retention policy configurable (time or size based).
  - No unbounded growth in local storage.
- **Dependencies:** GMR-001

### GMR-007 - Capture Modes and Bounded Queue
- **Priority:** P0
- **Owner:** Capture Specialist
- **Description:** Add `baseline` and `high-attention` capture modes with bounded queue/drop policy.
- **Acceptance Criteria:**
  - Mode switch changes capture cadence policy.
  - Queue depth is bounded and never grows unbounded.
  - Drop strategy documented (`latest-frame-wins` or equivalent).
- **Dependencies:** GMR-006

### GMR-008 - Reel Query API (Recent Window Retrieval)
- **Priority:** P1
- **Owner:** Capture Specialist
- **Description:** Add retrieval API for "last N seconds" and timestamp-adjacent moments.
- **Acceptance Criteria:**
  - Query returns ordered `ReelMoment` results.
  - Supports at least `last N seconds` and `nearest timestamp`.
  - Unit-level verification for timestamp boundaries.
- **Dependencies:** GMR-006

### GMR-009 - SharedContextEnvelope Builder
- **Priority:** P0
- **Owner:** LLM Context Specialist
- **Description:** Build context assembly pipeline using chat + session + reel references.
- **Acceptance Criteria:**
  - Envelope includes recent chat turns, session summary, active target metadata.
  - Reel references included when available.
  - Output always includes deterministic truncation metadata.
- **Dependencies:** GMR-001, GMR-008

### GMR-010 - Token Budgeting and Truncation Rules
- **Priority:** P0
- **Owner:** LLM Context Specialist
- **Description:** Enforce hard context budget with deterministic priority ordering.
- **Acceptance Criteria:**
  - Budget thresholds configured centrally.
  - Truncation order defined (e.g., oldest chat first, low-signal reel moments first).
  - No runtime overflow caused by context assembly.
- **Dependencies:** GMR-009

### GMR-011 - Session Summarization Loop
- **Priority:** P1
- **Owner:** LLM Context Specialist
- **Description:** Add rolling summary updates to maintain long-session coherence.
- **Acceptance Criteria:**
  - Summary updates on schedule or turn count threshold.
  - Summary is injected into context envelope.
  - Full chat replay is not required for coherence.
- **Dependencies:** GMR-009

### GMR-012 - Offline-Aware Chat UX ✅ (2026-02-20)
- **Priority:** P1
- **Owner:** Realtime/Infra Specialist
- **Description:** Improve send behavior and affordances when disconnected.
- **Acceptance Criteria:**
  - Send button disabled when disconnected. ✅
  - Clear inline hint shown when send is blocked. ✅ (Placeholder "Connect to send messages" when offline)
  - No silent discard of typed user input. ✅ (Guard in SendTextMessageAsync surfaces "Cannot send: not connected.")
- **Dependencies:** GMR-003

### GMR-013 - Retry Strategy (Auto + Manual)
- **Priority:** P1
- **Owner:** Realtime/Infra Specialist
- **Description:** Add one auto-retry and manual resend for failed user messages.
- **Acceptance Criteria:**
  - Failed send attempts one automatic retry.
  - Manual resend action available on failed messages.
  - Retry attempts are observable in message metadata.
- **Dependencies:** GMR-004

### GMR-014 - Chat Retention Cap
- **Priority:** P1
- **Owner:** Realtime/Infra Specialist
- **Description:** Cap in-memory chat list size for long sessions.
- **Acceptance Criteria:**
  - Configurable max message count (e.g., 200).
  - Oldest messages pruned when cap exceeded.
  - UI remains responsive in long runs.
- **Dependencies:** GMR-002

### GMR-015 - Integration Scenario + Acceptance Run
- **Priority:** P0
- **Owner:** Tech Lead + all specialists
- **Description:** Run end-to-end scenario: user text send, context build, AI response, reel reference.
- **Acceptance Criteria:**
  - Demo path works on default desktop target (1200x900).
  - User sees proper delivery states and AI reply in-thread.
  - At least one context build includes reel references.
  - Known defects logged with owner and severity.
- **Dependencies:** GMR-002, GMR-003, GMR-006, GMR-009

---

## Suggested Sprint Breakdown

### Sprint 1 (1 week) - MVP Functional Path
- GMR-001, GMR-002, GMR-003, GMR-004, GMR-005
- GMR-006, GMR-007
- GMR-009, GMR-010
- GMR-015 (initial run)

### Sprint 2 (1-2 weeks) - Reliability + Hardening
- GMR-008, GMR-011
- GMR-012, GMR-013, GMR-014
- GMR-015 (final acceptance run)

---

## Out of Scope for MVP (Explicit)

- Persistent chat history across app restarts
- Full semantic retrieval index for reels
- Advanced message taxonomy beyond `User`, `AiInsight`, `System`
- Typing indicators, edit/delete, search/export
- Multi-thread conversation views

---

## Weekly Rituals

- **Daily 15-min sync:** blockers across capture/context/infra.
- **Mid-week integration check:** verify contracts still aligned.
- **End-of-week acceptance pass:** execute GMR-015 scenario.

