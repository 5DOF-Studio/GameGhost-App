# Chat V2 MVP — PM Deliverable

**Audience:** Solo dev + Claude workflow  
**Context:** MainView V2 UI in place; Chat UI exists with message types (AI Insight, Warning, Lore, User) and input bar. Backend gaps identified.  
**Stack:** .NET MAUI, IConversationProvider (OpenAI/Gemini/Mock)

---

## 1. Chat V2 MVP Definition

**What must work for first usable release:**

| # | Capability | Definition |
|---|------------|------------|
| 1 | **User can send text** | User types in "Ask Gaimer..." and taps send → message appears in feed → provider receives it and can respond |
| 2 | **User sees AI replies** | Provider `TextReceived` → message appears in feed as AI Insight with correct styling |
| 3 | **User sees failures in-context** | Connection/API errors surface as system message in chat (not only debug log) |
| 4 | **Session feels coherent** | Messages persist for duration of session (no clear-on-navigation unless user chooses Clear) |
| 5 | **Basic delivery feedback** | User message shows "sending…" then "sent" or "failed" before AI response (or explicit error) |

**MVP excludes:** History across app restarts, retention caps, Warning/Lore message types from backend, typing indicators, message editing.

---

## 2. Prioritized Backlog (P0 / P1 / P2)

### P0 — Must have for MVP

| ID | Item | Rationale |
|----|------|-----------|
| P0-1 | Add `SendTextAsync` to `IConversationProvider` and wire providers | Without it, user text never reaches AI; MVP is incomplete |
| P0-2 | Wire `MainViewModel.SendTextMessage` to `_conversationProvider.SendTextAsync` | Connects UI to backend; enables round-trip |
| P0-3 | Add delivery state (Sending / Sent / Failed) to `ChatMessage` | Users need feedback when send fails vs succeeds |
| P0-4 | Surface `ErrorOccurred` as in-thread System message | Errors must be visible in chat, not hidden in logs |

### P1 — High value, within MVP window

| ID | Item | Rationale |
|----|------|-----------|
| P1-1 | Structured typed backend event (`IConversationMessage` / `ChatMessageDto`) | Enables Warning/Lore/System types later without breaking changes |
| P1-2 | Disable send when offline or empty; show offline hint | Prevents confusing "sent" when nothing was delivered |
| P1-3 | Retry on send failure (single retry + clear error) | Reduces frustration from transient network hiccups |

### P2 — Post-MVP

| ID | Item | Rationale |
|----|------|-----------|
| P2-1 | Persistence / history (e.g., SQLite, file) | Sessions across restarts; deferred to reduce scope |
| P2-2 | Retention cap (e.g., last N messages) | Performance/UX polish; not critical for first release |
| P2-3 | Mock provider emits typed messages (Warning, Lore scripts) | Useful for UI/UX validation; MVP uses AiInsight only |
| P2-4 | Message editing / deletion | Nice-to-have; not in first usable release |

---

## 3. User Stories + Acceptance Criteria (Top 5)

### US-1: Send text to AI (P0-1, P0-2)

**As a** user in an active session,  
**I want to** type a message and tap send so that the AI receives it and can respond.

**Acceptance criteria:**
- [ ] `IConversationProvider` defines `Task SendTextAsync(string text)`
- [ ] OpenAI provider implements via `conversation.item.create` with `input_text`
- [ ] Mock provider adds message to mock script / echoes as AI response
- [ ] Gemini provider implements if API supports text input (or no-op with clear UI state)
- [ ] `SendTextMessage` calls `SendTextAsync` after adding User message
- [ ] AI response appears in chat when provider fires `TextReceived`

---

### US-2: See delivery state (P0-3)

**As a** user sending a message,  
**I want to** see whether my message is sending, sent, or failed.

**Acceptance criteria:**
- [ ] `ChatMessage` has `DeliveryState` enum: `Unknown`, `Sending`, `Sent`, `Failed`
- [ ] User message shows `Sending` immediately; `Sent` after `SendTextAsync` completes; `Failed` on exception
- [ ] Failed messages show error text (e.g., `Source` or inline)
- [ ] UI differentiates states (e.g., opacity, icon, or subtle label)

---

### US-3: See errors in chat (P0-4)

**As a** user when something goes wrong,  
**I want to** see an error message in the chat feed, not only in the console.

**Acceptance criteria:**
- [ ] `ErrorOccurred` handler adds `ChatMessage` with `Type = System` (or new type) and error text
- [ ] System/error messages render with distinct styling (muted, smaller, or icon)
- [ ] Errors appear in correct chronological order in the feed
- [ ] No duplicate errors for same event (idempotent handling)

---

### US-4: Structured typed events (P1-1)

**As a** developer extending chat,  
**I want** backend events to carry message type and metadata so we can support Warning/Lore without re-architecting.

**Acceptance criteria:**
- [ ] New event `IConversationProvider.MessageReceived` with `ChatMessage` or equivalent DTO
- [ ] DTO includes `Type`, `Text`, optional `Source`, `Timestamp`
- [ ] `TextReceived` retained for backward compatibility; `MessageReceived` preferred for new consumers
- [ ] MainViewModel subscribes to `MessageReceived` and maps to `ChatMessages` with correct type

---

### US-5: Offline-aware send (P1-2)

**As a** user when disconnected,  
**I want** the send button disabled and a clear hint that I need to connect first.

**Acceptance criteria:**
- [ ] Send button disabled when `!IsConnected` or `string.IsNullOrWhiteSpace(MessageDraftText)`
- [ ] Placeholder or label indicates "Connect to send" when offline
- [ ] No "sending" state for messages when offline; button simply does not submit

---

## 4. Execution Plan (2 Phases)

### Phase A (≈1 week) — Core round-trip + visibility

| Task | Scope | Output |
|------|-------|--------|
| A1 | Add `SendTextAsync` to interface; implement in OpenAI (conversation.item.create), Mock (echo), Gemini (if supported) | Text flows to backend |
| A2 | Wire `SendTextMessage` → `SendTextAsync`; ensure `TextReceived` populates chat | End-to-end text chat works |
| A3 | Add `DeliveryState` to `ChatMessage`; update `SendTextMessage` to set Sending→Sent/Failed | User sees delivery feedback |
| A4 | Handle `ErrorOccurred` → add System message to `ChatMessages` | Errors visible in chat |

**Phase A done when:** User can send text, see AI reply, see delivery state, and see errors in chat.

---

### Phase B (≈1–2 weeks) — Structure + polish

| Task | Scope | Output |
|------|-------|--------|
| B1 | Introduce `MessageReceived` with typed payload; migrate MainViewModel | Ready for Warning/Lore |
| B2 | Disable send when offline; add "Connect to send" hint | Offline-aware UX |
| B3 | Single retry on `SendTextAsync` failure + clear error state | Fewer dead-ends on transient failures |
| B4 | (Optional) Mock provider script mode for Warning/Lore | Better manual testing |

**Phase B done when:** Typed events exist, offline UX is clear, and retry reduces send failures.

---

## 5. Risks + Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| OpenAI/Gemini API changes for text input | Low | High | Pin to known API version; add integration test |
| Mock provider out of sync with real providers | Medium | Low | Keep Mock minimal; document behavior in code |
| DeliveryState complicates UI templates | Low | Medium | Use triggers on single template; avoid extra templates |
| Error flood (e.g., reconnect loop) | Medium | Medium | Debounce/throttle ErrorOccurred → System messages |
| Scope creep (history, retention) | High | Medium | Explicit out-of-scope; Phase C backlog |

---

## 6. Explicit Out-of-Scope for MVP

| Excluded | Reason |
|----------|--------|
| **Chat history across app restarts** | Requires persistence layer; Phase C |
| **Retention cap (max N messages)** | Performance/UX polish; not needed for first release |
| **Backend-driven Warning/Lore types** | Depends on AI provider features; MVP = AiInsight + User + System |
| **Typing indicator** | Nice-to-have; adds provider protocol work |
| **Message edit/delete** | Not in first usable release |
| **Search/filter in chat** | Post-MVP |
| **Export chat** | Post-MVP |
| **Multi-conversation / threads** | Architectural; out of scope |

---

## Summary

- **MVP:** Send text → see AI reply → see delivery state → see errors in chat. No persistence.
- **Phase A (1 week):** `SendTextAsync` + wiring + delivery state + error surfacing.
- **Phase B (1–2 weeks):** Typed events + offline UX + retry.
- **Out of scope:** History, retention cap, Warning/Lore from backend, typing indicator, edit/delete.

---

*Document created: 2026-02-20*
