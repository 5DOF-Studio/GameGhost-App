# BUG-017 Live Test Checklist

**Feature:** Brain Error Sanitization + Auto-Disconnect
**Date built:** 2026-03-25
**Branch:** gaimer-v2
**Tests:** 1120 passed, 0 failed, 12 skipped (net8.0)

---

## Prerequisites
- [ ] OpenRouter API key set and credits available
- [ ] Build + deploy to /Applications with signing
- [ ] Connect to a chess game session with Leroy or Wasp

## Test Scenarios

### 1. Happy Path (OpenRouter healthy)
- [ ] Connect session, let brain analyze 3-5 frames
- [ ] Confirm no error messages appear on timeline or chat
- [ ] Confirm brain analysis flows normally to TopStrip and timeline

### 2. Transient 5xx Recovery
- [ ] Trigger a transient failure (if reproducible, or wait for one)
- [ ] Confirm: brief pause visible (retries happening in background)
- [ ] Confirm: no raw HTTP/JSON error text visible anywhere
- [ ] Confirm: session continues if retry succeeds

### 3. Sustained 5xx / Auto-Disconnect
- [ ] Trigger sustained failure (e.g., invalid model, or wait for OpenRouter outage)
- [ ] Confirm: error message reads "Brain service is temporarily unavailable after N attempts"
- [ ] Confirm: connection indicator goes inactive (session disconnects)
- [ ] Confirm: chat panel shows post-disconnect system message
- [ ] Confirm: timeline keeps the single sanitized error event
- [ ] Confirm: no duplicate error toasts (dedup working)

### 4. Rate Limit (429) Handling
- [ ] Trigger rapid requests to hit rate limit
- [ ] Confirm: retries occur with backoff
- [ ] Confirm: message reads "Brain is rate-limited after N attempts" (not raw 429)
- [ ] Confirm: auto-disconnect fires on exhaustion

### 5. Auth Failure (401/403)
- [ ] Temporarily set invalid API key, then connect
- [ ] Confirm: immediate disconnect (no retries)
- [ ] Confirm: message reads "Brain authentication failed. Check the OpenRouter key, then reconnect."
- [ ] Confirm: no raw API key or auth error text visible

### 6. Reconnect After Auto-Disconnect
- [ ] After any auto-disconnect scenario above, reconnect
- [ ] Confirm: clean reconnect, no stale error state
- [ ] Confirm: brain analysis resumes normally
- [ ] Confirm: previous error message remains visible in chat

### 7. Ghost Mode Error Handling
- [ ] Enter ghost mode, trigger brain error
- [ ] Confirm: error appears in ghost card/SpineCard (not hidden main view)
- [ ] Confirm: auto-disconnect still fires in ghost mode
- [ ] Confirm: FAB returns to idle state after disconnect

### 8. Query Path (In-Game Chat)
- [ ] While connected, send a text chat message during brain outage
- [ ] Confirm: sanitized error in chat (not raw exception)
- [ ] Confirm: auto-disconnect fires if retries exhausted

## Telemetry Verification
- [ ] Check JSONL session trace for `brain.request.retry` events with attempt counts
- [ ] Check for `brain.request.failure` events with fingerprints
- [ ] Check for `brain.disconnect_requested` event on auto-disconnect
- [ ] Confirm no raw error payloads in trace output

## Pass Criteria
All scenarios above produce sanitized user-facing messages. No raw API errors, HTTP status codes, or exception types visible to the user. Auto-disconnect fires reliably. Reconnect works cleanly.
