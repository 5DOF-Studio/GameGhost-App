# GAIMER Desktop — Deployment Roadmap

**Project:** Witness Desktop (.NET MAUI — macOS Catalyst + Windows)
**Created:** 2026-02-25 (Tuesday night)
**Deadline:** 2026-02-27 (Friday)
**Working Days Remaining:** 2 (Wednesday + Thursday)
**Author:** 5DOF AI Studio

---

## Executive Summary

| Metric                  | Value           |
|-------------------------|-----------------|
| Total tasks             | 28              |
| Completed (shipped)     | 22              |
| Remaining (this sprint) | 6 must-have     |
| Should-have             | 5               |
| Nice-to-have            | 6               |
| Deferred (code review)  | 5               |
| Est. hours (must-have)  | 14-18h          |
| Est. hours (should-have)| 10-14h          |
| Buffer                  | ~0h (tight)     |

The must-have tasks are achievable in two focused days. Should-have tasks are stretch goals — include any that fit but do not sacrifice must-have quality. Nice-to-have and deferred items ship post-Friday.

---

## Friday Submission Criteria

The app is **ready to submit** when all of the following are true:

1. **Real AI Provider Connected** — At least one real provider (Gemini Live or OpenAI Realtime) successfully sends/receives audio, text, and image data through the full pipeline.
2. **API Keys Secured** — Keys are not hardcoded in source. At minimum, environment variables with a documented setup process. Ideally, platform-native secure storage (macOS Keychain / Windows DPAPI).
3. **Error Resilience** — Network disconnect, auth failure, and rate limit scenarios do not crash the app. User sees a meaningful error message in-thread and can reconnect.
4. **Full Flow Smoke Test Passes** — Agent Select > Game Select > Connect > Screen Capture running > AI receives frames + responds > Direct message exchange > Disconnect > Resources released. No crashes, no leaked processes.
5. **Windows Build Compiles** — `dotnet build -f net8.0-windows10.0.19041.0` succeeds. Runtime validation is a should-have, but compilation is non-negotiable.
6. **macOS Build Runs End-to-End** — The ditto + codesign workflow produces a launchable .app that completes the full flow above.
7. **Session Cleanup** — Disconnect stops capture timer, disconnects provider WebSocket, releases audio engine, and returns to MainView cleanly.

**Submission deliverable:** Working macOS .app bundle + Windows build artifacts + README with setup instructions (API key config, build commands, known limitations).

---

## Completed Work (Shipped)

These are done and verified. No further action needed.

| # | Feature | Phase |
|---|---------|-------|
| 1 | Agent Selection UI (portrait cards, chevron nav, color glows) | 01 |
| 2 | MainView V2 UI (chat-forward, timeline, sidebar preview/connectors) | 01 |
| 3 | MinimalView (compact HUD, inline messages, auto-dismiss) | 01 |
| 4 | Chat Brain Architecture (BrainEventRouter, SessionManager, TimelineFeed, ChatPromptBuilder) | 02 |
| 5 | Timeline hierarchy (Checkpoint > EventLine > TimelineEvent, capsule UI) | 02 |
| 6 | Icon mapping system (16 unique icons, EventIconMap, color-coded capsules) | 02 |
| 7 | IConversationProvider abstraction (Gemini, OpenAI, Mock implementations) | 02 |
| 8 | Audio pipeline — NAudio (Windows) + AVAudioEngine (macOS) — mic capture + playback | -- |
| 9 | Screen Capture — ScreenCaptureKit (GPU/Metal, native Swift xcframework) | 03 |
| 10 | Screen Capture — CGDisplayCreateImage fallback (pre-macOS 14) | 03 |
| 11 | Screen Capture — Window enumeration via CGWindowListCopyWindowInfo | 03 |
| 12 | P/Invoke bridge (DllImport resolver, GCHandle delegate pinning) | 03 |
| 13 | Memory leak fixes (UIImage, NSData, NSString, NSArray disposal) | 03 |
| 14 | TCC permission resolution (Apple Development cert + entitlements) | 03 |
| 15 | macOS deploy pipeline (/Applications/ + codesign + entitlements) | 03 |
| 16 | Direct message chat (user text input > AI response in thread) | 02 |
| 17 | Proactive alerts (DANGER/OPPORTUNITY with urgency levels) | 02 |
| 18 | Out-game chat support | 02 |
| 19 | Preview image binding (ByteArrayToImageSourceConverter) | 02 |
| 20 | Session state machine (OutGame/InGame with tool gating) | 02 |
| 21 | Build-in-public documentation (SDK spec, capture story, chronicles) | -- |
| 22 | BrainContextService (SharedContextEnvelope, token budgeting, context assembly) | 02 |

---

## Must-Have for Friday

These are blocking submission. Work these first, in priority order.

| # | Task | Status | Est. Hours | Target Day | Notes |
|---|------|--------|------------|------------|-------|
| M1 | Test with real Gemini Live API | Not Started | 3-4h | Wed | Replace mock provider, verify audio + image + text flow end-to-end. Requires `GEMINI_API_KEY` env var. Test with Apple Chess.app as target. |
| M2 | Test with real OpenAI Realtime API | Not Started | 3-4h | Wed | Same verification as M1. Requires `OPENAI_API_KEY` env var. Validate the MacCatalyst playback-speed fix (BUG-008) on real audio. |
| M3 | API key management (secure storage) | Not Started | 2-3h | Wed-Thu | **Minimum:** env vars with documented setup. **Better:** macOS Keychain via `SecKeychain` / Windows DPAPI via `ProtectedData`. Add first-run prompt if no key found. |
| M4 | Error handling for real provider connections | Not Started | 2-3h | Thu | Handle: WebSocket disconnect, HTTP 401/403 (bad key), HTTP 429 (rate limit), network timeout. Surface errors as in-thread system messages (GMR-005 pattern already exists). Add try-catch wrappers in `GeminiConversationProvider` and `OpenAIConversationProvider`. |
| M5 | Verify Windows build compiles and runs | Not Started | 2-3h | Thu | `dotnet build -f net8.0-windows10.0.19041.0` must succeed. Fix any macOS-only code paths (SCK, AVAudioEngine) behind platform guards. Screen capture is macOS-only — stub or gate on Windows. |
| M6 | Session end cleanup + full smoke test | Not Started | 2-3h | Thu-Fri | Disconnect must: stop capture timer, close WebSocket, release audio engine, clear buffers. Then run full flow: Agent Select > Connect > Capture > AI response > Direct message > Disconnect. No crashes. |

**Total estimated: 14-20 hours across 2 days**

### Recommended Schedule

**Wednesday (Feb 26):**
- Morning: M1 — Gemini Live API integration test (3-4h)
- Afternoon: M2 — OpenAI Realtime API integration test (3-4h)
- Evening: M3 — API key management (2-3h)

**Thursday (Feb 27, morning):**
- Morning: M4 — Error handling wrappers (2-3h)
- Midday: M5 — Windows build verification (2-3h)
- Afternoon: M6 — Cleanup + smoke test (2-3h)

**Thursday evening / Friday morning:**
- Final smoke test pass
- Package submission deliverables
- Write setup README

---

## Should-Have (Improves Submission Quality)

Work these only after all must-haves are green. Pick based on available time.

| # | Task | Status | Est. Hours | Target Day | Notes |
|---|------|--------|------------|------------|-------|
| S1 | Persistence layer (SQLite) | Not Started | 4-6h | Stretch | Save/restore timeline events + chat messages across sessions. EF Core + SQLite. Schema exists in `chat-brain-design.md` Section 8. |
| S2 | Session replay (past sessions) | Not Started | 2-3h | Stretch | Depends on S1. Load historical sessions from SQLite, display in Timeline. |
| S3 | Connection retry logic | Not Started | 1-2h | Thu | Auto-reconnect on transient WebSocket failures. Exponential backoff (1s, 2s, 4s, max 30s). Cap at 5 retries then surface error. |
| S4 | Provider switching without restart | Not Started | 1-2h | Thu | Allow changing between Gemini/OpenAI in settings while disconnected. Currently requires env var change + restart. |
| S5 | Audio device selection | Not Started | 2-3h | Stretch | Select input/output devices instead of system default. Research complete (`GAIMER/GameGhost/AUDIO_DEVICE_SELECTION_RESEARCH.md`). |

**Total estimated: 10-16 hours (likely only S3 and S4 fit)**

---

## Nice-to-Have (Ship Without)

These do not block submission. Tackle post-Friday.

| # | Task | Status | Est. Hours | Target Day | Notes |
|---|------|--------|------------|------------|-------|
| N1 | Progressive capsule compression animation | Not Started | 3-4h | Post-Friday | Timeline capsules compress as they age. MAUI animations. |
| N2 | Wasp (FPS) agent portrait asset | Not Started | 1h | Post-Friday | Need portrait PNG. Agent code exists but is gated. |
| N3 | Stable developer certificate | Not Started | 2-3h | Post-Friday | Replace Apple Development cert with Developer ID for distribution. |
| N4 | App icon and branding polish | Not Started | 2-3h | Post-Friday | Custom app icon, launch screen, about dialog. |
| N5 | Windows screen capture implementation | Not Started | 8-12h | Post-Friday | Windows Graphics Capture API. macOS-only for now — document as known limitation. |
| N6 | SCK thumbnail generation | Not Started | 2-3h | Post-Friday | Replace CGDisplayCreateImage thumbnails with SCK-native. Lower priority — current approach works. |

---

## Deferred Code Review Items

From Phase 03 code review. Technical debt — schedule post-submission.

| # | Task | Severity | Est. Hours | Notes |
|---|------|----------|------------|-------|
| D1 | IAsyncDisposable for conversation providers | H3 | 1-2h | Proper async cleanup pattern for WebSocket resources. |
| D2 | WaveOutEvent dispose-on-stop (Windows audio) | H4 | 0.5h | NAudio resource leak on Windows when stopping playback. |
| D3 | SessionManager non-atomic state mutation | M2 | 1-2h | Race condition risk on rapid state transitions. Add lock or use Interlocked. |
| D4 | Magic strings for game type to constants | M3 | 0.5h | Replace `"chess"`, `"rpg"`, `"fps"` with `GameType` enum or constants. |
| D5 | SendContextualUpdateAsync should be system-level context | M5 | 1h | Currently sent as assistant message — should be system/context injection. |

---

## Risk Register

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| **Gemini Live API key issues** — provisioning delays, quota limits, or API changes since last tested | Medium | High | Have OpenAI Realtime as fallback. If both fail, submit with mock + documented integration code. |
| **OpenAI Realtime API breaking changes** — WebSocket protocol may have updated since Dec 2025 implementation | Medium | High | Check OpenAI changelog. Provider is already implemented and was tested Dec 15 — likely stable but verify. |
| **Windows build fails** — macOS-specific P/Invoke or framework references break Windows compilation | High | Medium | Platform guards (`#if MACCATALYST`) already exist for SCK. May need additional guards for AVAudioEngine paths. Budget 2-3h for Windows fixes. |
| **Screen capture permission denied on fresh machine** — TCC prompt not handled gracefully | Low | Medium | Already resolved with Apple Development cert + entitlements. Document the permission flow in README. |
| **Audio echo/feedback loop with real API** — mic captures AI playback audio | Medium | Medium | BUG-009 (echo suppression) flagged as pending validation. If severe, add manual push-to-talk as fallback. |
| **Rate limiting on real API** — heavy capture + chat exceeds API quotas | Medium | Low | Capture interval is 30s (not aggressive). Add rate limit detection in M4 error handling. |
| **Time crunch — must-haves take longer than estimated** — debugging real API integration is unpredictable | Medium | High | Prioritize: get ONE provider working fully (Gemini preferred) before attempting second. A single working provider is sufficient for submission. |

### Contingency Plan

If time runs short, the minimum viable submission is:

1. **One real provider working** (Gemini Live preferred — it handles audio + vision natively)
2. **Env var API key setup** (skip Keychain/DPAPI, document in README)
3. **Basic error handling** (try-catch with in-thread error messages, no retry logic)
4. **macOS build only** (document Windows as "compiles but untested" if M5 takes too long)
5. **Manual cleanup** (document "restart app between sessions" if M6 cleanup is incomplete)

This gets the submission in the door. Polish comes in the week after.

---

## Architecture Reference

```
Agent Selection UI ──> MainView V2 ──> MinimalView (HUD)
                           │
                    ┌──────┴──────┐
                    │  Connect    │
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              v            v            v
         Audio Engine   Screen Capture  IConversationProvider
         (mic + play)   (SCK/CGImage)   (Gemini / OpenAI / Mock)
              │            │            │
              └────────────┼────────────┘
                           v
                   BrainEventRouter
                   ├── OnScreenCapture()
                   ├── OnDirectMessage()
                   └── OnImageAnalysis()
                           │
              ┌────────────┼────────────┐
              v            v            v
         TimelineFeed   Voice Output   ProactiveAlerts
         (UI timeline)  (audio play)   (DANGER/OPPORTUNITY)
```

**Key integration points for Must-Have tasks:**
- M1/M2: Replace `MockConversationProvider` selection in `ConversationProviderFactory.cs` — real providers already implemented, need API keys and end-to-end validation.
- M3: `ConversationProviderFactory.cs` already reads env vars (`GEMINI_API_KEY`, `OPENAI_API_KEY`). Upgrade to Keychain/DPAPI if time permits.
- M4: Wrap WebSocket operations in `GeminiConversationProvider.cs` and `OpenAIConversationProvider.cs` with try-catch; surface via existing `ErrorOccurred` event.
- M5: Check `GaimerDesktop.csproj` target frameworks; ensure `#if MACCATALYST` guards cover all native interop.
- M6: `MainViewModel.ToggleConnectionCommand` is the disconnect entry point — verify it chains: provider disconnect > capture stop > audio release.

---

## File Quick Reference

| Purpose | Path |
|---------|------|
| Solution | `src/GaimerDesktop/GaimerDesktop.sln` |
| Main project | `src/GaimerDesktop/GaimerDesktop/GaimerDesktop.csproj` |
| DI registration | `src/GaimerDesktop/GaimerDesktop/MauiProgram.cs` |
| Provider factory | `src/GaimerDesktop/GaimerDesktop/Services/Conversation/ConversationProviderFactory.cs` |
| Provider interface | `src/GaimerDesktop/GaimerDesktop/Services/Conversation/IConversationProvider.cs` |
| Provider impls | `src/GaimerDesktop/GaimerDesktop/Services/Conversation/Providers/` |
| Brain router | `src/GaimerDesktop/GaimerDesktop/Services/BrainEventRouter.cs` |
| Session manager | `src/GaimerDesktop/GaimerDesktop/Services/SessionManager.cs` |
| Timeline feed | `src/GaimerDesktop/GaimerDesktop/Services/TimelineFeed.cs` |
| Main ViewModel | `src/GaimerDesktop/GaimerDesktop/ViewModels/MainViewModel.cs` |
| Brain context | `src/GaimerDesktop/GaimerDesktop/Services/BrainContextService.cs` |
| Chat brain spec | `GAIMER/GameGhost/gaimer_spec_docs/chat-brain-design.md` |
| Implementation roadmap | `GAIMER/GameGhost/gaimer_spec_docs/implementation-roadmap.md` |
| Screen capture arch | `GAIMER/GameGhost/gaimer_spec_docs/SCREEN_CAPTURE_ARCHITECTURE.md` |

---

*Last updated: 2026-02-25 (Tuesday). Next update: 2026-02-26 (Wednesday EOD) after M1-M3 status.*
