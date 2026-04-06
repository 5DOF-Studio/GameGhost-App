# Live Test Checklist

**Created:** 2026-04-02  
**Context:** Multiple sessions of deferred live testing. Covers Phase 12 Audio Intelligence (31 commits), Replay Pipeline (10 commits), BUG fixes (017-020), and accumulated UX work.

**Deploy command:**
```bash
dotnet build src/WitnessDesktop/WitnessDesktop/WitnessDesktop.csproj -f net8.0-maccatalyst -p:EnableCodeSigning=false && rm -rf /Applications/Gaimer.app && ditto --norsrc src/WitnessDesktop/WitnessDesktop/bin/Debug/net8.0-maccatalyst/maccatalyst-x64/Gaimer.app /Applications/Gaimer.app && codesign --force --deep --sign "Apple Development: Ike Nlemadim (DCRQMPF7A9)" --entitlements scripts/WitnessDesktop.entitlements /Applications/Gaimer.app && open /Applications/Gaimer.app
```

---

## Tier 1: Deploy Only (No Credits Needed)

These can be verified immediately after a build + deploy. No API keys required.

### App Launch + UI
- [ ] App launches without crash
- [ ] Agent selection page loads (Leroy, Wasp, RASA visible)
- [ ] Main page renders with dark theme, custom fonts
- [ ] Settings page accessible
- [ ] **BARGE IN toggle** visible on audio control bar (was GAME AUDIO)
- [ ] BARGE IN toggle is OFF by default
- [ ] Toggling BARGE IN on/off persists across page navigation

### Ghost Mode (No Connection)
- [ ] Enter ghost mode — FAB appears with agent portrait
- [ ] Exit ghost mode — MAUI window restores
- [ ] FAB is draggable on all surfaces
- [ ] Ghost panel positions on correct monitor
- [ ] Ghost mode gear icon accessible

### Exchange State Machine (No Voice)
- [ ] App starts in TextOnly degradation mode (no exchange gating)
- [ ] No crash when typing chat messages (exchange system is dormant)

### SFX Player
- [ ] `affirmation_ping.mp3` file present in bundle (check app contents if possible)

---

## Tier 2: Gemini API Only (GEMINI_API_KEY in .env)

Replay pipeline uses Gemini directly — no OpenRouter credits needed.

### Replay Recording (Phase 1 — already verified Apr 2)
- [x] FPS 25-29fps stable
- [x] HEVC Main codec (hvc1)
- [x] 2:30 rotation with gapless double-buffered swap
- [x] Segments at `~/Library/replays/<sessionId>/`

### Replay Analysis (Phase 2)
- [ ] Connect to any game window
- [ ] Play for 2:30+ to complete at least one segment
- [ ] Verify GeminiVideoClient uploads segment to Gemini Files API
- [ ] Verify SqliteSegmentAnalysisStore receives analysis result
- [ ] Check console for `[ReplayAnalysis]` log lines confirming auto-analysis
- [ ] Circuit breaker: if Gemini returns 429, automated analysis pauses after 3 failures

### Replay Search (Phase 3)
- [ ] With at least one analyzed segment, ask agent: "what happened earlier?"
- [ ] Agent should trigger `search_replay` tool
- [ ] Verify FTS5 store is searched first (instant, zero Gemini cost)
- [ ] If FTS5 miss, verify Gemini Pro fallback fires
- [ ] Agent narrates replay search results naturally

---

## Tier 3: OpenRouter Credits Required

These need `OPENROUTER_API_KEY` with active credits. Use chess game for brain pipeline testing.

### Brain Pipeline Basics
- [ ] Connect to chess game window (chess.com or Lichess)
- [ ] Brain captures frames and analyzes (TopStrip shows position text)
- [ ] Timeline shows structured events: Assessment, Danger, SageAdvice
- [ ] Emission queue drip-feeds at ~2.5s intervals (not burst)
- [ ] Brain model is `google/gemini-2.5-flash` (check console logs)

### Voice + Exchange (Phase 12A)
- [ ] Connect voice (OpenAI Realtime)
- [ ] Degradation mode transitions to Full (voice + brain)
- [ ] Say "Hey Leroy" — **wake ping plays** (affirmation_ping.mp3 at low volume)
- [ ] Exchange opens — agent responds to directed speech
- [ ] Silence for 15s — exchange closes naturally (verify via console `[exchange] closed`)
- [ ] During active exchange, agent responds to board questions with grounded context
- [ ] Without wake phrase, agent does NOT respond to ambient speech
- [ ] Say "Hey Leroy" again — new exchange opens (different ExchangeId in logs)

### Wake Phrase Reliability
- [ ] "Hey Leroy" — detected (exact match)
- [ ] "hey leroy" (quiet/casual) — detected (case insensitive)
- [ ] "Hey Larry" — detected (fuzzy Levenshtein, distance 2)
- [ ] "What's up Leroy" — NOT detected (no "Hey" prefix)
- [ ] "Hey there" — NOT detected (wrong name, distance too far)
- [ ] Try with Wasp: "Hey Wasp" — detected
- [ ] Try with RASA: "Hey RASA" — detected

### Fuzzy Wake Phrase Edge Cases
- [ ] "Hey wait" with Wasp active — note if false positive fires (known limitation, distance 2)
- [ ] If false positive rate is high, document for Porcupine priority escalation

### Voice Delivery Gating (Phase 12A)
- [ ] During active exchange: brain hints delivered to voice
- [ ] During dormant exchange: brain hints suppressed (silent, TopStrip still updates)
- [ ] Interrupt priority (blunder) still delivers even when dormant

### Barge-In (Phase 12C)
- [ ] Enable BARGE IN toggle
- [ ] During dormant exchange, brain produces a proactive alert
- [ ] If user is SILENT: agent barges in and speaks the alert
- [ ] If user is SPEAKING: alert is suppressed (queued as reminder)
- [ ] Disable BARGE IN → no more unsolicited speech

### Reminder Queue (Phase 12C)
- [ ] With BARGE IN disabled, let brain produce alerts during dormant exchange
- [ ] Say "Hey Leroy" to open new exchange
- [ ] Agent should surface ONE reminder from the queue ("Earlier I noticed...")
- [ ] Stale reminders (>5 min old) should NOT be surfaced

### Voice-Brain Deferral (Phase 12D)
- [ ] During active exchange, say "What happened earlier?" (HistorySensitive)
- [ ] Agent says stock deferral: "Let me check the footage on that"
- [ ] Exchange transitions to AwaitingBrain (silence timer resets)
- [ ] Brain processes the request (may take 5-15s)
- [ ] If exchange still active: answer delivered via voice
- [ ] If exchange expired during wait: answer queued as reminder
- [ ] During active exchange, say "Run the engine on this" (ToolDependent)
- [ ] Agent says: "Running the engine on this position"

### Silence Timer Presets
- [ ] With chess pack active: verify 15s Normal timeout
- [ ] (If CoD pack available): verify 8s Quick timeout

### Agent Speech Tracking (Phase 12B)
- [ ] While agent is speaking, new brain results are held (not double-talking)
- [ ] After agent finishes, queued results can deliver

### Ghost Mode + Exchange
- [ ] Enter ghost mode while connected
- [ ] Say "Hey Leroy" — wake ping plays, exchange opens
- [ ] Ghost FAB should receive exchange state (check console for `[GhostMode] ghost_panel_set_exchange_state`)
- [ ] Agent speech appears on ghost card
- [ ] Brain analysis still routes to ghost notification rotation

---

## Tier 4: Bug Verification (OpenRouter Required)

### BUG-017: Brain Error Sanitization
- [ ] Trigger brain error (e.g., invalid model, rate limit)
- [ ] Error message is sanitized (no raw API error text shown to user)
- [ ] Retry with exponential backoff visible in logs
- [ ] After 4 retries: terminal error fires, brain pauses
- [ ] Error dedup: same error within 15s window not repeated
- [ ] Post-error: connector stays up, chat preserved
- [ ] See full checklist: `WITNESS/BUG-017-LIVE-TEST-CHECKLIST.md`

### BUG-018: Ghost Toggle Double-Fire
- [ ] In ghost mode, tap voice chat toggle rapidly
- [ ] Toggle should fire exactly once per tap (250ms dedup)
- [ ] No double-fire or state oscillation

### BUG-019: Ghost Alert Routing
- [ ] In ghost mode, trigger unsupported audio feature
- [ ] Alert should appear on ghost card (not just timeline)
- [ ] Non-ghost mode: alert fires normally via timeline

### BUG-020: Brain Failure Pauses Only
- [ ] Trigger terminal brain error during active session
- [ ] Brain analysis pauses (CancelAll only)
- [ ] Connector stays UP (not disconnected)
- [ ] Capture stays UP
- [ ] Chat messages preserved (not cleared)
- [ ] Voice toggle stays ON visually after ghost audio sync

### BUG-014: Voice Grounding
- [ ] During chess game with voice active
- [ ] Ask board-sensitive question: "Am I winning?"
- [ ] Agent responds with grounded context (references actual position)
- [ ] Grounding cooldown: same context not injected repeatedly (12s dedup)
- [ ] Stale context (>45s): agent expresses uncertainty

### BUG-013: Tool-Call Visibility
- [ ] Brain triggers tool call (e.g., analyze_position_engine)
- [ ] Tool call appears in timeline with muted blue treatment
- [ ] Tool call appears in ghost card tool section
- [ ] Tool call duration label visible

---

## Tier 5: Blocked (Awaiting External)

### Porcupine Wake Word (PICOVOICE_ACCESS_KEY)
- [ ] Obtain access key from Picovoice Console
- [ ] Set `PICOVOICE_ACCESS_KEY` in `.env`
- [ ] Verify `IsAvailable = true` in console logs
- [ ] Test built-in "PORCUPINE" wake word (detection on raw audio)
- [ ] Train custom wake words: "Hey Leroy", "Hey Wasp", "Hey RASA"
- [ ] Bundle `.ppn` files in Resources/WakeWords/
- [ ] Verify Porcupine detects custom wake words
- [ ] Compare detection latency: Porcupine vs fuzzy transcript
- [ ] Run 6 skipped Porcupine tests (remove Skip annotations)

---

## Live Testing Findings (2026-04-02 Session)

Issues encountered during first live test after Phase 12 deployment.

### Found and Fixed This Session

| Issue | Symptom | Root Cause | Fix | Commit |
|-------|---------|------------|-----|--------|
| RASA announced personality | "Here to roast and keep tabs" on first response | No anti-pattern rule against self-description | Added "NEVER announce personality" to prompt + anti-patterns | `f2cb185` |
| Called player "RASA" | Agent greeted user with its own name | No rule against using agent name as greeting | Added "NEVER call player '{Name}'" to prompt | `f2cb185` |
| "Checking the board" for non-chess | RASA (general agent) said "checking the board" | Grounding correction hardcoded chess language in MainViewModel | Pack-driven `GroundingLanguage` — each pack defines its own terms | `03eb41d` |
| Interruption not working | Agent kept talking when user spoke over it | `Interrupted` event declared but never raised in OpenAIRealtimeService | Added `_isResponseActive` tracking, fire `Interrupted` + `response.cancel` on `speech_started` during active response | `3bd7224` |
| Voice responded without wake word | Agent responded to all speech, not just "Hey RASA" | Prompt-level "stay silent" not enforceable with OpenAI server_vad `create_response: true` | Added `[CRITICAL — WAKE PHRASE REQUIRED]` to system prompt. Partially effective — server_vad still triggers responses. Full fix requires Porcupine audio-level gating. | `3463b06` |
| Voice had no game state awareness | Agent made in-game claims when out-of-game | System prompt set once at connect, never updated | Added `UpdateInstructionsAsync` — `session.update` on InGame/OutGame transitions | `ae68709` |
| Double output | Every voice message displayed twice in chat | OpenAI provider fired BOTH `TextReceived` + `MessageReceived` for same text; MainViewModel subscribed to both | Provider now fires `MessageReceived` only (structured path) | `ab50070` |
| Chess-specific turn classification | VoiceGroundingCoordinator used chess regex for all agents | Hardcoded `BoardSensitiveRegex` with chess terms (fork, pin, castle) | Pack-driven `VoiceClassificationPatterns` — each pack contributes its own patterns, generic fallback | `d9a95e0` |

### Known Limitations (Tracking)

| Issue | Severity | Status | Blocked On |
|-------|----------|--------|------------|
| **Wake phrase unreliable** | High | Mitigated (prompt enforcement) | Porcupine access key. Prompt enforcement is partial — OpenAI server_vad `create_response: true` triggers responses regardless of instructions. Full fix: Porcupine audio-level detection + audio gate on SendAudioAsync when dormant. |
| **"Hey wait" false positive for Wasp** | Medium | Known | Porcupine. Levenshtein distance 2 matches "hey wait" → "hey wasp". Acceptable for V1. |
| **VoiceGroundingCoordinator chess patterns** | Medium | **FIXED** | Pack-driven `VoiceClassificationPatterns` in `d9a95e0`. Chess pack has chess patterns, CoD pack has FPS patterns, generic fallback for packless agents. |
| **ExchangeOpened/Closed SFX not verified** | Low | Untested | Requires wake word to work reliably to test exchange lifecycle |
| **Barge-in not verified** | Low | Untested | Requires OpenRouter credits + reliable exchange lifecycle |
| **Reminder surfacing not verified** | Low | Untested | Requires barge-in → queued reminders → exchange open cycle |
| **~~Exchange never fires~~** | ~~High~~ | **RESOLVED** | Was failing in earlier build. After double-output fix (`ab50070`), exchange lifecycle works: wake → ping → open → conversation → silence → close SFX → dormant. VAD confirmed working excellently. |
| **Screen recording FPS degraded** | Medium | Investigating | FPS drops from 2.1fps to 0.4fps after voice connects. Was 25-29fps in Phase 1 verification (no voice active). CPU contention from audio + voice + recording running simultaneously. |
| **RSS climbing toward 500MB** | Medium | Monitoring | 393MB at launch → 488MB after 2min with session active. Climbing ~1.5MB/min. Possible memory leak in audio buffers or screen recording pipeline. |
| **`response.cancel` on non-active response** | Low | Known | `"Cancellation failed: no active response found"` in trace. `speech_started` fires interrupt when no response is active. Needs guard: only cancel if `_isResponseActive`. |
| **Brain 402 (credits exhausted)** | External | Blocked | `openrouter:http_402` — OpenRouter credits empty. Brain pauses after 1 attempt. All brain-dependent testing blocked until credits replenished. |

### Architecture Observations

**Communication Protocol Ownership (who classifies what):**

The spec says "Voice owns brain. Brain answers to voice." Current ownership:

- **Voice-side (correct):** Turn classification (`VoiceGroundingCoordinator`), exchange lifecycle (`ExchangeManager`), delivery decisions (`VoiceDeliveryGate`), capability awareness (`BrainCapabilityManifest`)
- **Shared (correct):** Game state (`SessionManager`), grounding language (`GameSkillPack.GroundingLanguage`)
- **Brain-side (correct):** Analysis style, tool selection, structured output format

**Resolved:** `VoiceGroundingCoordinator` now reads pack-driven classification patterns via `GameSkillPack.VoiceClassification`. Chess pack contributes chess patterns, CoD contributes FPS patterns. Generic fallback patterns (game-agnostic) used when no pack is active. Grounding *language* AND classification *patterns* are both pack-driven now.

---

## Verification Log

Record results here during live testing sessions.

| Date | Tier | Item | Result | Notes |
|------|------|------|--------|-------|
| 2026-04-02 | T2 | Replay Phase 1 | PASS | FPS stable, HEVC, 2:30 rotation verified |
| 2026-04-02 | T1 | App launches | PASS | No crash, agent selection loads |
| 2026-04-02 | T3 | Wake phrase | PARTIAL | Prompt enforcement active but server_vad bypasses it |
| 2026-04-02 | T3 | Interruption | FIXED | Was broken (event never fired), now fires on speech_started |
| 2026-04-02 | T3 | Personality announcement | FIXED | Added anti-pattern rules |
| 2026-04-02 | T3 | Agent name as greeting | FIXED | Added prompt rule |
| 2026-04-02 | T3 | Chess language for non-chess | FIXED | Pack-driven grounding language |
| 2026-04-02 | T3 | Game state awareness | FIXED | Runtime session.update on state change |
| 2026-04-02 | T3 | Double output | FIXED | OpenAI provider fired both events → single MessageReceived now |
| 2026-04-02 | T3 | Chess classification for non-chess | FIXED | Pack-driven VoiceClassificationPatterns |
| 2026-04-02 | T3 | Exchange lifecycle | **PASS** | Wake → ping → open → conversation → silence → close SFX → dormant. Full cycle verified. |
| 2026-04-02 | T3 | Screen recording FPS | DEGRADED | 0.4fps with voice active (was 25fps without voice). CPU contention. |
| 2026-04-02 | T3 | Memory (RSS) | WARNING | 488MB after 2min, climbing 1.5MB/min. Approaching 500MB target. |
| 2026-04-02 | T3 | Brain pipeline | BLOCKED | OpenRouter 402 — credits exhausted. Brain paused after 1 attempt. |
| 2026-04-02 | T3 | Personality announcement (2nd test) | IMPROVED | No more "snarky sidekick" but still says "documenting every questionable choice" — partial improvement |
| | | | | |
