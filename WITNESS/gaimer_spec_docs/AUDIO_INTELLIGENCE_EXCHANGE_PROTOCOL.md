# Audio Intelligence Exchange Protocol

**Project:** Gaimer / Witness Desktop  
**Branch intent:** Cloud-first, voice-first  
**Status:** Planning / protocol definition  
**Purpose:** Define the behavioral and architectural contract for voice-first interaction, exchange lifecycle, barge-in behavior, reminder carry-forward, and future virtual-mic routing.

## 1. Core Product Direction

Gaimer should behave as a voice-first companion, not as a brain-first system with spoken output.

That means:
- voice owns the user interaction
- brain serves voice
- brain does not independently define the user experience
- brain supplies context, analysis, tool results, and candidate outputs
- voice decides whether something is spoken now, deferred, routed as a reminder, or absorbed silently into context

This protocol is the control layer above raw audio capture and playback.

## 2. What "Audio Intelligence" Means Here

In this phase, audio intelligence means:
- wake-phrase-based directed interaction
- exchange-aware conversational state
- cache-first contextual responses
- async refresh while conversation continues
- reminder carry-forward when results arrive after the exchange closes
- user-controlled barge-in categories
- future routing of AI speech to a virtual microphone connector

Explicitly out of scope for this phase:
- interpreting general game audio with Whisper or another local STT model
- always-on passive semantic understanding of all ambient game audio
- platform-owned virtual audio driver creation

This protocol also assumes a distinction between:

- **stabilization work**
  - make live voice truthful, grounded, and observable
- **audio-intelligence feature work**
  - richer spoken orchestration, semantic rendering, reminders, and exchange policy

The protocol defines the long-term model. It does not imply that every part must land in the next grounding bug-fix rotation.

## 3. The Main Principle

> Voice owns brain. Brain answers to voice.

Operationally:
- voice is the orchestrator
- brain is a subordinate intelligence service
- the user addresses voice
- voice decides whether to ask brain for help
- brain returns data or candidate speech
- voice decides how and when that result enters the conversation

## 4. Exchange Model

### 4.1 Definition

An `Exchange` is a bounded interval in which the user is explicitly addressing the agent and expects direct response.

An exchange begins when:
- a wake phrase is detected, for example `Hey Leroy`

An exchange contains:
- directed user speech
- voice interpretation
- optional brain/context/tool requests
- one or more voice responses

An exchange ends when:
- silence timeout expires
- the interaction reaches a natural close
- the user stops directing speech at the agent

### 4.2 Why Exchange Exists

Gaming is noisy. The system should not treat all audio as directed command traffic.

The exchange model gives:
- explicit user intent
- fewer false activations
- better pacing of speech output
- safe deferral of late results
- a clean boundary between directed conversation and ambient play

## 5. Exchange Lifecycle

### 5.1 Proposed States

```text
Dormant
WakeDetected
ExchangeOpening
ExchangeActive
AwaitingBrain
ReminderQueued
ExchangeClosing
ExchangeExpired
```

### 5.2 State meanings

`Dormant`
- no directed interaction in progress
- ambient audio is ignored for command intent except wake detection

`WakeDetected`
- wake phrase detected
- system is preparing to open an exchange

`ExchangeOpening`
- exchange acknowledged
- voice can give a short acknowledgment if needed

`ExchangeActive`
- user is actively addressing the agent
- direct voice responses are allowed

`AwaitingBrain`
- exchange still active
- voice has asked brain/context/tools for help
- cache result may already have been spoken

`ReminderQueued`
- fresh result returned after exchange expired
- result should be preserved for future surfacing

`ExchangeClosing`
- final spoken turn / reminder wrap-up / graceful close

`ExchangeExpired`
- exchange closed
- no direct voice injection unless barge-in policy allows it

## 6. Wake Phrase Protocol

### 6.1 Required behavior

Near-term activation should assume directed speech begins with the agent name:
- `Hey Leroy`
- `Hey Annie`
- `Hey <AgentName>`

This is not yet a full wake-word engine. It is a directed-phrase gate.

### 6.2 Near-term implementation assumption

The practical first implementation can rely on transcript text from the active voice provider:
- detect whether transcript includes the wake phrase
- only then open an exchange

Longer-term, this may evolve into a dedicated wake-gate layer. That is not required to define the protocol.

### 6.3 Result

Without the wake phrase:
- no exchange opens
- no directed voice-response behavior is guaranteed
- late brain results should go to reminders or silent context unless barge-in policy allows interruption

## 7. Cache-First Then Refresh

### 7.1 Required response behavior

When a user asks for current state, voice should not stall waiting for fresh analysis if useful cached context exists.

Instead:
1. return cached context immediately
2. explicitly state freshness, for example:
   - `That was from 8 seconds ago`
   - `Last confirmed 12 seconds ago`
3. in parallel, fetch fresh context or complete the requested task
4. when the fresh result arrives:
   - if exchange is still active, inject it into the ongoing conversation
   - if exchange has expired, convert it into a reminder

### 7.2 Why this matters

This creates the right feel:
- immediate responsiveness
- honest freshness signaling
- no dead air while the brain works
- no wasted late answer after the player has moved on

### 7.3 Grounded fact envelope

The cache consumed by voice should not be a raw transcript dump. It should be an app-owned grounded fact envelope derived from brain output.

At minimum, that envelope should carry:

- factual summary
- freshness
- confidence
- whether a fresh read is required before speaking as fact

Later, the same envelope may also carry semantic event classes from the brain:

- `Danger`
- `Opportunity`
- `Assessment`
- `ImageAnalysis`
- `SageAdvice`

Voice should be aware of those classes structurally, but it should usually translate them into natural speech style rather than speaking the labels literally.

## 8. Reminder Model

### 8.1 Definition

A `Reminder` is a deferred brain/voice result that became relevant during or after an exchange but was not spoken immediately because:
- the exchange expired
- the user stopped directing speech at the agent
- barge-in conditions were not met

### 8.2 Reminder sources

Likely reminder sources:
- refreshed context arriving after exchange expiry
- tool execution results arriving after exchange expiry
- low-priority callouts
- deferred free commentary

### 8.3 Reminder behavior

When the next exchange opens, voice may:
- preface with the most relevant pending reminder
- include it in the last turn before exchange close
- merge it into a directly relevant answer

Default recommendation:
- do not front-load all reminders at exchange start
- surface only the most relevant reminder first
- keep reminder delivery short and compressible

### 8.4 Reminder lifecycle

```text
Candidate result -> not spoken now -> Reminder queued
Reminder queued -> next exchange relevant -> spoken
Reminder queued -> becomes stale -> dropped
Reminder queued -> superseded by fresher result -> replaced
```

## 9. Barge-In Model

### 9.1 Product requirement

`Barge In` must be controllable mid-session on the live audio toggle bar.

It should replace a weak/non-functional toggle such as `Game Audio`.

### 9.2 UX split

Live audio bar:
- `Barge In` on/off

Settings page:
- detailed barge-in conditions checklist

So the runtime behavior is:
- in-session toggle answers: `Can the agent speak unprompted right now?`
- settings answer: `What kinds of unprompted speech are allowed when barge-in is enabled?`

### 9.3 Barge-in categories

The allowed categories are:
- `Reminder`
- `ToolExecution`
- `CallOut`
- `FreeCommentary`

### 9.4 Category definitions

`Reminder`
- deferred result from a prior exchange
- usually low urgency, but useful continuity

`ToolExecution`
- operational progress/result messages such as:
  - `Searching the internet`
  - `Updating journal`
  - `Finished checking that`

`CallOut`
- situational moments voice believes should be surfaced
- not assumed to be perfect “critical moment” detection
- can include danger/opportunity style notifications when supported
- chess-specific examples include:
  - `you are in check`
  - `mate threat on the next move`
  - `there is a tactic here right now`

`FreeCommentary`
- unsolicited personality-driven commentary
- lowest priority from a competitive/noise perspective

### 9.5 Output modes

This naturally creates two macro modes:

`ExchangeOnly`
- no unsolicited speech
- reminders remain queued

`ExchangePlusSelectedBargeIn`
- unsolicited speech allowed, but only for selected categories

## 10. Exchange vs Barge-In Authority

The system should follow this order of precedence:

1. If exchange is active:
   - direct response path wins
   - voice may inject fresh results immediately

2. If exchange is inactive and barge-in is disabled:
   - no unsolicited speech
   - candidate output becomes reminder or silent context

3. If exchange is inactive and barge-in is enabled:
   - only selected barge-in categories may speak
   - everything else becomes reminder or silent context

## 11. Voice Output Decision Matrix

```text
Input arrives ->
Is exchange active?
  Yes -> speak if relevant to current exchange
  No ->
    Is barge-in enabled?
      No -> queue reminder or absorb silently
      Yes ->
        Is category allowed?
          Yes -> speak
          No -> queue reminder or absorb silently
```

## 12. Tool Execution Behavior

Tool execution should not default to noisy play-by-play.

Preferred rule:
- if tool execution is part of an active exchange, voice may give short operational feedback
- if tool execution completes after exchange expiry, result becomes reminder unless barge-in allows `ToolExecution`

Short example phrases:
- `Checking that now`
- `Searching the internet`
- `Updating journal`
- `Done checking`

## 13. Brain Contract Under Voice Ownership

Brain should expose at least these classes of output to voice:
- cached context snapshot
- fresh context result
- tool execution progress/result
- candidate narration
- result freshness metadata
- urgency/category metadata

But brain should not decide final delivery.

Brain produces:
- what happened
- how fresh it is
- how important it may be
- what category it belongs to

Voice decides:
- speak now
- speak later
- add to reminder queue
- absorb into silent context

## 14. Exchange-Aware Brain Requests

When voice asks brain for help, requests should carry exchange context:
- exchange id
- request timestamp
- whether cached response already delivered
- expiry time / silence timeout boundary
- current barge-in policy

This allows returned results to be evaluated against current conversation state.

## 15. Fresh Result Handling

When fresh analysis/task completion arrives:

If exchange still active:
- inject into current exchange
- possibly as correction or refinement to cached response

If exchange expired:
- do not abruptly speak unless barge-in category is allowed
- queue as reminder

If exchange still active but topic has drifted:
- voice may summarize rather than replay full result

## 16. Reminder Prioritization

When multiple reminders exist, ranking should likely favor:
1. freshness
2. direct relevance to current exchange
3. urgency/category
4. whether the reminder supersedes an older reminder

Suggested rule:
- keep reminder queue small
- replace stale/superseded reminders aggressively
- do not let reminders become a second timeline backlog

## 17. Virtual Mic Connector

### 17.1 Product direction

AI speech should eventually be routable not only to speakers, but also to a selected virtual microphone device.

This enables Gaimer to act like an intelligent voice layer for multiplayer/voice platforms when the user already has a virtual mic installed.

### 17.2 Why connector model is correct

Treating `Virtual Mic` as a connector is stronger than treating it as an internal audio trick.

It:
- matches the app’s connector philosophy
- makes the route explicit
- lets users choose among existing devices
- avoids first requiring Gaimer to ship its own virtual audio driver

### 17.3 Connector concept

New connector type:
- `VirtualMicConnector`

This connector represents:
- an output route for AI-generated speech audio
- typically backed by an existing virtual microphone device on the host machine

### 17.4 Routing options

Voice output routes should be modeled explicitly:
- `SpeakersOnly`
- `VirtualMicOnly`
- `SpeakersAndVirtualMic`
- `SilentContextOnly`

### 17.5 Example flow

```text
User: "Hey Leroy, tell them I’m rotating left"
Exchange opens
Voice interprets request
Brain/context may help if needed
AI speech generated
Output route = SpeakersAndVirtualMic
-> user hears it locally
-> selected virtual mic connector receives the same AI audio
-> game/voice app hears Gaimer through virtual mic
```

### 17.6 Safety concerns

This feature requires strong guardrails:
- avoid speaker -> mic -> loopback feedback
- make output route visible and user-controlled
- show when virtual mic connector is selected/active
- avoid always-on unsolicited broadcasting
- respect exchange/barge-in policy

### 17.7 Near-term implementation direction

Near-term approach:
- integrate with existing virtual mic devices already installed by the user
- expose them as connector candidates
- do not take on custom driver creation first

## 18. Proposed Runtime Entities

Likely runtime entities for this system:
- `ExchangeSession`
- `ExchangeState`
- `ReminderItem`
- `ReminderQueue`
- `BargeInCategory`
- `BargeInPolicy`
- `VoiceOutputRoute`
- `VirtualMicConnector`
- `WakePhrasePolicy`

## 19. Proposed Ownership Split

### Voice orchestrator owns
- wake phrase gate
- exchange open/close
- direct response pacing
- reminder surfacing
- barge-in policy enforcement
- final spoken delivery decision

### Brain owns
- analysis
- tools
- context assembly
- freshness metadata
- candidate narration
- urgency/category suggestion

### Router/context layer owns
- transport of results
- storage of context
- timeline/ghost distribution

## 20. Proposed Near-Term UX Changes

### Audio bar

Replace weak/non-functional `Game Audio` toggle with:
- `Barge In`

### Settings

Add:
- `Barge In Conditions`
  - Reminder
  - Tool Execution
  - Call-out
  - Free Commentary

### Connectors

Future connector addition:
- `Virtual Mic`

### Voice freshness language

Standardize phrases like:
- `Last confirmed 6 seconds ago`
- `That was from 11 seconds ago — checking again now`

## 21. Design Constraints

This protocol must remain consistent with the existing canonical rule:

> Brain is the sole consumer of visual data. Voice never sees raw images.

Nothing in this design changes that.

Voice remains voice-first without becoming a second vision pipeline.

## 22. Recommended Next Documents

After this protocol, the next useful documents would be:

1. `AUDIO_INTELLIGENCE_STATE_MACHINE.md`
- concrete runtime states and transitions

2. `VIRTUAL_MIC_CONNECTOR_IMPLEMENTATION_PLAN.md`
- discovery, routing, device selection, safety, and platform constraints

3. `BARGE_IN_POLICY_AND_REMINDER_QUEUE_SPEC.md`
- detailed policy tables and queue behavior

## 23. Voice-Brain Sync Insights (Apr 2, 2026)

These observations emerged from building the replay pipeline (Phases 1-3) and examining how `search_replay` — a brain-only tool — interacts with voice-first interaction.

### 23.1 The Tool-Dependent Question Gap

Brain now has 9 tools, including `search_replay` which searches past gameplay footage. Voice has zero tools. When a user asks voice "what happened at B site?", voice has no way to:

1. Know that this question requires a tool it doesn't have
2. Signal brain to prioritize this query
3. Defer its response until brain has the answer
4. Follow up when brain's answer arrives

The existing `VoiceGroundingCoordinator` classifies turns as `BoardSensitive` (needs current board state) but has no concept of `ToolDependent` (needs a tool voice doesn't have) or `HistorySensitive` (needs past footage/journal).

**Proposed: Lightweight voice-side tool awareness.**
Voice doesn't need to call tools, but it needs a manifest of what brain can do. A `BrainCapabilityManifest` (not the full tool schema — just names + one-line descriptions + trigger phrases) would let voice say "Let me check the footage on that" or "I'll run the engine on that position" instead of confabulating or going silent.

This aligns with Section 3 ("voice decides whether to ask brain for help") but requires voice to know what help is available.

### 23.2 The Transcript Timing Gap

The Voice Transcript Bridge (Mar 27) captures user speech and feeds it to brain via `SharedContextEnvelope.RecentVoiceTranscript`. But brain reads transcripts on its next capture cycle — up to 5 seconds later. By then voice has already responded.

```
T+0s: User says "what happened at B site?"
T+0.3s: Voice hears it, starts responding (generic or confabulated)
T+3-5s: Brain's next cycle reads transcript, sees the question
T+5-10s: Brain calls search_replay, gets answer
T+10s: Brain pushes result via SendContextualUpdateAsync
T+10s: But voice has moved on. The answer arrives as context injection,
       not as a continuation of the user's question.
```

**Proposed: Priority request channel.**
When voice detects a question it can't answer (via the tool-awareness manifest or turn classification), it should write a `BrainRequest` to a dedicated channel — not just the transcript store. Brain would read this channel with higher priority than its regular capture cycle. This is the `AwaitingBrain` state from Section 5.1, made concrete.

The request should carry:
- The user's question (text)
- Which brain capability is likely needed (e.g., "search_replay", "analyze_position_engine")
- Exchange ID (from Section 14)
- Whether voice has already given a deferral response ("Checking the footage...")

### 23.3 The Deferral-to-Delivery Gap

Even with the Exchange Protocol, there's a missing mechanism for voice to "resume" a deferred question. Today:

1. Voice says "Let me check on that" (deferral)
2. Brain gets the answer 10 seconds later
3. Brain pushes it via `SendContextualUpdateAsync`
4. Voice receives it as generic context injection — not tagged as "this is the answer to the question you deferred"

The result is that voice might never narrate the answer, or might weave it into an unrelated response.

**Proposed: Tagged brain responses.**
When brain fulfills a `BrainRequest`, the response should carry:
- The original request ID / exchange ID
- A flag: `is_deferred_answer = true`
- A pre-formatted voice narration (brain already generates `VoiceNarration` in `BrainResult`)

Voice should recognize tagged responses and either:
- Inject immediately if exchange is still active (Section 15: "inject into current exchange")
- Queue as a high-priority reminder if exchange expired (Section 8)
- Use the `ToolExecution` barge-in category to deliver it unprompted

### 23.4 Turn Classification Expansion

Current `VoiceTurnClass` enum: `Social`, `Control`, `GeneralGameQuestion`, `BoardSensitive`, `Unclear`

Proposed additions based on the tool ecosystem:

| New Class | Trigger Patterns | Response Mode |
|-----------|-----------------|---------------|
| `HistorySensitive` | "what happened", "how did I die", "show me that play", "earlier", "last round" | Defer to brain (search_replay) |
| `ToolDependent` | "run the engine", "check my journal", "search for" | Defer to brain (specific tool) |

These would map to `VoiceResponseMode.DeferToBrain` — a new response mode where voice gives a deferral acknowledgment and writes a `BrainRequest`.

### 23.5 Incremental Implementation Path

These insights suggest a three-step path, each independently shippable:

**Step 1: Prompt engineering (zero code)**
Add to voice system prompt: "If the user asks about past gameplay events, say 'Let me check the footage on that' — your brain companion will search the replay and update you shortly."
Brain already sees transcripts and has `search_replay`. The gap is UX expectations only.

**Step 2: Turn classification + deferral (2-3 tasks)**
- Add `HistorySensitive` and `ToolDependent` to `VoiceTurnClass`
- Add `DeferToBrain` response mode to `VoiceGroundingCoordinator`
- Voice gives stock deferral, transcript store marks turn as `needs_brain_response`
- Brain prioritizes marked turns on next cycle

**Step 3: Request-response bridge (Phase 12 core)**
- `BrainRequest` channel (voice → brain, priority)
- Tagged `BrainResponse` (brain → voice, with exchange ID)
- Voice resumes deferred questions when tagged response arrives
- Maps to `AwaitingBrain` state (Section 5.1)

Steps 1-2 are pre-Phase-12 stabilization work. Step 3 is the Phase 12 implementation.

### 23.6 Relationship to Existing Spec Sections

| Insight | Existing Coverage | Gap |
|---------|------------------|-----|
| Voice needs tool awareness | Section 3 says "voice decides whether to ask brain for help" | Voice doesn't know what help brain can offer |
| Priority request channel | Section 14 defines exchange-aware brain requests | No concrete mechanism for voice to initiate requests |
| Deferral-to-delivery | Section 15 covers fresh result handling | No tagging to match response to deferred question |
| Turn classification | VoiceGroundingCoordinator classifies turns | Missing HistorySensitive and ToolDependent classes |
| Reminder for late answers | Section 8 defines reminder model | Not connected to specific deferred questions |

## 24. Design Decisions Locked (Apr 2, 2026)

These decisions were resolved through design review and are binding for Phase 12 implementation.

### D-AI-1: Connection Lifecycle — Warm Pause, Not Teardown

Exchange ending pauses the exchange state but does NOT tear down the WebSocket connection. The connection stays warm. This preserves the sub-second response latency that justified using the Realtime API.

```
Exchange ends (silence timer fires)
  ├── WebSocket: stays alive (warm)
  ├── Exchange state: reset to Dormant
  ├── Conversational context: cleared/refreshed for next exchange
  └── Next wake phrase: instant activation, no reconnect penalty
```

### D-AI-2: VAD / Echo Cancellation Strategy

Multi-layered approach, supplementing platform-native AEC:

1. **Mute mic / stop sending audio data while agent `IsSpeaking = true`** — prevents echo feedback loop
2. **Use native platform audio input trackers** to detect user speech or mic input
3. **On user speech detection:** reduce agent audio volume + send interrupt signal to voice provider
4. **This fires internal state management** to support VAD-aware exchange behavior
5. **External dependencies acceptable:** Porcupine (wake word), WebRTC AEC (echo cancellation)

### D-AI-3: Silence Timer — Exchange Death Timer Only

Silence is not conversational pacing. It is solely the timer that determines when an exchange ends.

- Silence = no speech from either party for N seconds
- Timer fires → exchange transitions to Dormant
- Any speech (user or agent) resets the timer
- Prompts will instruct the voice agent to stay silent within an exchange if unsure it's being spoken to (no filler chatter to keep the exchange alive)
- Timer duration configurable per GameSkillPack (Quick: 8s, Normal: 15s, Patient: 30s)

### D-AI-4: Barge-In — Only When User Is Silent

Agent never barges in while the user is speaking. Simple rule, no exceptions for V1.

```
Barge-in event arrives
  ├── User is speaking? → suppress entirely (notification conversion is future refinement)
  └── User is silent?
      ├── Exchange is active? → speak (resets silence timer)
      └── Exchange is dormant? → speak (opens new exchange, starts silence timer)
```

The silence timer becomes the universal heartbeat. Any speech resets it. Barge-in during user silence effectively extends or opens an exchange. Agent barge-in speech keeps the exchange alive by resetting the timer.

### D-AI-5: Interrupted Thoughts — Value Decays, Pipeline Handles It

No special queuing for interrupted agent speech. Information has a relevance half-life — value should be evaluated at delivery time, not generation time.

The brain's continuous analysis cycle naturally handles this:
- Position changed → next analysis supersedes the interrupted thought
- Position unchanged → insight remains valid and informs the next response through context
- Interrupted content is not re-spoken or explicitly queued
- The agent's next response incorporates whatever is still relevant from its internal state

Over time, urgency and value can be weighted to determine if stale information deserves surfacing. But for V1: let the pipeline's natural refresh cycle handle it.

### D-AI-6: Teammate Voice Chat — V1 Limitation

V1 does not attempt speaker identification or audio source separation. The wake phrase gate ("Hey {AgentName}") is the primary mechanism to distinguish agent-directed speech from teammate chatter.

Documented limitation: "Gaimer works best with headphones or when game voice chat uses a separate audio device." The wake phrase + exchange timeout handles 80% of cases. The remaining 20% is an acceptable V1 trade-off.

### D-AI-7: Wake Phrase — Open to External Dependency

Transcript-regex matching is acceptable as MVP. Porcupine (Picovoice) is acceptable as a production dependency for on-device wake word detection with:
- Custom wake words per agent name
- <5ms detection latency
- Works offline (no cloud dependency for activation)

Decision on which approach to use will be made during Phase 12 planning based on integration complexity and .NET compatibility.

### D-AI-8: Windows Parity — Design for Both, Build Mac First

All Audio Intelligence interfaces and abstractions must be platform-agnostic. Platform-specific implementations live behind interfaces, following the existing pattern (e.g., `IAudioService` → `MacCatalyst.AudioService` / `Windows.AudioService`).

During Phase 12, for every native capability used on macOS, document what the Windows equivalent would be:

| macOS | Windows Equivalent | Notes |
|-------|-------------------|-------|
| AVAudioEngine (mic input) | NAudio / WASAPI | NAudio is the standard .NET audio library for Windows |
| AVAudioSession (routing) | MMDevice API | Windows audio endpoint selection |
| CoreAudio (VAD/levels) | WASAPI capture levels | Or NAudio WaveIn with level metering |
| Porcupine (wake word) | Porcupine (.NET SDK) | Cross-platform — same library works on both |
| WebRTC AEC (if used) | WebRTC AEC | Cross-platform — same library |
| Virtual mic routing | VB-Cable / Virtual Audio Cable | Third-party virtual audio devices on Windows |

The interface layer must not leak platform assumptions. If macOS uses `AVAudioEngine.inputNode.installTap`, the interface is `IAudioInputProvider.StartCapture()` — Windows implements the same interface with NAudio.

### D-AI-9: Voice Activity Display Animation

The VAD meter (currently 12-bar visualization in Ghost FAB) must evolve to support exchange state:

| Exchange State | VAD Display Behavior |
|---------------|---------------------|
| Dormant | Bars idle/dim — subtle breathing animation |
| Wake Detected | Bars pulse once (acknowledgment) |
| Exchange Active (user speaking) | Bars animate with mic input levels (current behavior) |
| Exchange Active (agent speaking) | Bars animate with agent output levels (inverted — show agent is talking) |
| AwaitingBrain | Bars pulse slowly (thinking indicator) |
| Dormant + Barge-in pending | Bars glow briefly before agent speaks |

The animation should communicate who is talking and whether the system is processing. This is the primary visual feedback for voice state — especially important when the user can't tell if the agent heard them.

Implementation: the existing `GhostFabTokens` animation layer in AppKit supports this. The VAD bars are CALayer-based with configurable heights. Exchange state drives the animation mode, mic/output levels drive the bar heights within that mode.

## 25. Summary

The intended system is:
- wake-gated
- exchange-based
- voice-first
- cache-first then refresh
- reminder-aware
- user-controlled for unsolicited speech
- extensible to virtual mic routing through connector selection

Voice owns the interaction.
Brain serves voice.
Late results do not get wasted.
Unsolicited speech becomes a controlled product behavior, not an accident.
