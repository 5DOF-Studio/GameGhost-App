# Cloud Live Verification Checklist

**Branch:** `post-mini-dev`  
**Purpose:** Manual verification gate for the cloud-first continuation branch after post-MiniCPM cleanup, timeline UX changes, ghost-mode fixes, and rebootstrap groundwork.

## Scope

Verify the active cloud path only:
- cloud brain
- cloud voice
- capture and timeline flow
- ghost-mode presentation flow

Out of scope for this checklist:
- LocalOnly / LocalFirst runtime behavior
- retention or virtualization behind the `Archived` marker
- ghost icon freedom-of-movement implementation

## Preflight

Before launching:
- `InferenceMode` should be `CloudOnly`
- `VoiceProvider` should be the provider you intend to test (`gemini` recommended first)
- `USE_MOCK_SERVICES` must be unset or not `true`
- cloud API keys must be present for the chosen voice path and for OpenRouter brain

Expected current cloud stack:
- brain: `OpenRouter (anthropic/claude-sonnet-4)` or current configured OpenRouter brain model
- voice: `Gemini Live` preferred first pass

Useful log surfaces:
- `/tmp/gaimer-debug.log`
- app console output

Key startup signals to confirm:
- `[BrainFactory] Mode=CloudOnly`
- `[Gaimer][DI] IBrainService=OpenRouter ...`
- `[ConversationProviderFactory] Selected GeminiConversationProvider ...` or `OpenAIConversationProvider ...`

Immediate red flags:
- `Mock Brain`
- `Mock Provider`
- `no API keys found`
- `IConversationProvider` or `IBrainService` resolution failure

## Manual Pass

### 1. Startup and provider selection

Verify:
- app launches without falling into mock mode
- settings diagnostics read cloud-facing, not local-facing
- no stale local/fallback messaging leaks into visible UX

Expected:
- cloud provider names are reflected in diagnostics/logs
- no visible `LocalOnly` / `LocalFirst` product path in normal UX

### 2. Connect and basic session start

Verify:
- selecting agent + target allows connection
- connect state changes correctly
- no laggy-feeling mismatch between intent and visible state on session start

Expected:
- connect transitions to connected state cleanly
- no immediate system error event

### 3. Voice path

Verify:
- mic permission and session startup work
- user speech is accepted
- assistant voice/text response arrives

Expected:
- live voice response without falling back to mock
- no repeated realtime websocket/provider errors

Failure signatures to capture:
- Gemini websocket / receive loop errors
- OpenAI response failed / empty-response patterns
- audio session activation errors

### 4. Text path

Verify:
- typed message routes correctly
- user message appears in timeline
- assistant response appears as latest expanded message, not truncated capsule

Expected:
- newest message is readable as the active bubble
- older messages compress after newer ones arrive

### 5. Tool-call legibility

Trigger a path that can invoke a tool if available.

Verify:
- timeline shows a distinct tool-call event
- tool icon and action summary read clearly
- tool-use does not look like normal commentary

Expected:
- examples like `Searched Internet` or `Updating journal`
- tool events appear before final related reply when appropriate

### 6. Ghost mode

Verify:
- enter ghost mode feels immediate
- exit ghost mode feels immediate
- tool use in ghost mode does not render through the plain text-card path

Expected:
- tool-use card uses icon-led presentation
- exit clears visible ghost active state immediately even if native teardown lags

### 7. Timeline archived boundary

Verify:
- `Archived` appears as a grey centered boundary marker
- it reads as end-of-scroll history boundary, not normal commentary
- it does not attach visually to only the newest checkpoint

Expected:
- centered marker at checkpoint level
- no bottom-anchored chat feel introduced

## Failure Triage

If a failure occurs, capture:
- exact user action
- whether failure is startup, connect, voice, text, tool, ghost mode, or timeline
- screenshot if visual
- relevant lines from `/tmp/gaimer-debug.log`

Fast interpretation:
- `Mock Brain` or `Mock Provider`: configuration / key-selection failure, not core UI regression
- `Brain error: HTTP ...`: cloud brain reachable but request/model/auth failed
- websocket receive / unexpected errors: voice-provider transport issue
- correct logs but wrong UI rendering: likely frontend/presentation regression

## Likely First-Fix Ownership

- provider selection / bootstrap / runtime / diagnostics: Codex
- timeline / settings / ghost card rendering / visible polish: Claude
- native ghost movement work: separate dedicated session, not part of this live gate

## Exit Condition

This verification pass is complete when:
- cloud startup is confirmed non-mock
- one successful connect/disconnect cycle passes
- one successful text roundtrip passes
- one successful voice roundtrip passes
- tool-call visibility is confirmed if tool invocation occurs
- ghost-mode presentation is validated
- archived boundary rendering is validated
