# Gaimer: The AI Gaming Companion

## What Is Gaimer?

Gaimer is a desktop application that gives every gamer a personal AI companion — one that watches your screen, understands what's happening in your game, and talks to you about it in real time through voice and text. It sits as a translucent overlay on top of your game, sees every move, tracks every pattern, and delivers insights the moment they matter.

Think of it as the teammate who's always watching, always thinking, and never tilts.

Gaimer isn't a bot that plays for you. It's a copilot. It watches, analyzes, and advises — but you make the moves. It's the difference between a GPS that drives the car and one that tells you the fastest route. You're still in control. The AI just makes sure you're never playing blind.

---

## The Problem

Gaming is one of the last major entertainment categories where AI hasn't meaningfully shown up at the player level. Millions of gamers play complex, high-stakes games every day — chess, shooters, RPGs, strategy games — and the help available to them is limited to static guides, YouTube tutorials from last year, and Discord advice from strangers who may or may not know what they're talking about.

The gap is real-time intelligence. While you're mid-game, making decisions under pressure, there's no one watching over your shoulder who actually understands the position and can tell you what to do right now. Coaching exists, but it's expensive, scheduled, and human-limited. What if that coaching intelligence could be instant, always available, and personalized to exactly how you play?

That's Gaimer.

---

## How It Works

### The Brain-Voice Pipeline

At the heart of Gaimer is a system we call the Brain-Voice Pipeline — the core architecture that makes real-time game intelligence possible.

**The Brain sees.** Gaimer captures your game window using native screen capture (ScreenCaptureKit on macOS, Windows Graphics Capture on Windows). It polls at 1 Hz and emits a frame whenever the visual content changes — so the brain sees every move within a second of it happening. The brain is the sole consumer of visual data. Nothing else in the system touches raw images.

**The Brain thinks.** The vision model analyzes the screenshot: reads the chess board, identifies the minimap layout, spots the health bars, interprets the inventory screen — whatever the game demands. It produces structured analysis: what's happening, what changed, what's dangerous, what's opportune. It can call tools — a local chess engine, a game state tracker, a web search for build guides — to enrich its analysis with authoritative data.

**The Brain speaks.** Analysis flows through a Channel pipeline to a Router that distributes it to three outputs simultaneously:
- **Timeline** — a persistent event feed showing everything the brain has observed during the session
- **Voice** — natural language narration delivered through the voice agent, who speaks the analysis in character
- **Ghost Mode** — a floating overlay card that appears on top of your fullscreen game with the latest insight

The voice agent never sees raw images. It receives text — the brain's analysis, translated into natural speech. This separation is fundamental: the brain is the analyst, the voice is the storyteller. They're connected by text, not pixels.

### Three-Layer Memory

The brain doesn't just analyze individual screenshots. It maintains a layered memory system that builds context over time:

**L1 — Immediate (0-30 seconds).** What just happened. "Knight moved to f6." "Enemy flanked from the east corridor." High-fidelity, short-lived observations from the latest analysis.

**L2 — Rolling Summary (30 seconds to 5 minutes).** What's been happening. "Player has been trading pieces aggressively in the last 2 minutes." "Team keeps losing the B objective." Trends and patterns synthesized from recent L1 events.

**L3 — Session Narrative (5+ minutes).** The story of the session. "Started strong but lost momentum after move 20." "Improved rotations after being coached on positioning." Strategic arc used for long-term coaching continuity.

When the voice agent needs context — to answer a question, to decide whether to speak up — it pulls a budget-truncated packet from these layers. It gets exactly enough context to be useful without being overwhelmed. The brain remembers everything; the voice gets the highlights.

---

## The Agents

Gaimer doesn't have one personality. It has agents — distinct AI characters, each designed for a specific game genre, with their own personality, expertise, tools, and way of seeing the game.

### Why Agents, Not One AI

Different games require fundamentally different kinds of intelligence. A chess coach needs to read board positions, evaluate pawn structures, and calculate forcing lines. An FPS companion needs to read minimaps, track cooldowns, call out flanking routes, and react in sub-second timeframes. An RPG advisor needs to understand skill trees, crafting systems, quest objectives, and narrative context.

One generic AI can't do all of this well. But a specialized agent — primed with the right tools, the right visual vocabulary, and the right personality for its domain — can be exceptional at one thing.

### How Agents Are Built

Each agent is composed of five personality blocks that define who they are:

1. **SOUL** — Core identity, worldview, and philosophy. What drives them. How they see their role.
2. **STYLE** — How they talk. Voice cadence, vocabulary, humor style, emotional range.
3. **BEHAVIOR** — Operating rules. When to speak, when to stay silent, how to handle mistakes, priority order.
4. **SITUATIONS** — Specific game scenarios and exactly how to respond. The chess agent knows what to do during a blunder; the FPS agent knows what to do during a clutch round.
5. **ANTI-PATTERNS** — What the agent must never do. Guardrails that prevent common AI failure modes.

These personality blocks are injected into every LLM touchpoint — the brain's vision analysis, the voice agent's conversation, and the text chat. The agent's character is consistent whether it's analyzing a screenshot, speaking aloud, or typing a response.

### Genre-Specific Tools

Each agent carries a toolbelt specific to its game genre. Tools are gated by session state — some are always available, others only activate when you're connected to a game.

**Chess agents (Leroy, Wasp) carry:**
- `analyze_position_engine` — Calls a local Stockfish chess engine for authoritative position evaluation. Returns the best move, centipawn evaluation, and continuation lines. This is the ground truth.
- `analyze_position_strategic` — Asks the AI to explain the position in human terms. Why is this move good? What's the strategic idea? What should the player be thinking about?
- `get_game_state` — Reads the current session context: FEN position, move history, whose turn it is, game phase.
- `capture_screen` — Triggers a fresh screenshot and brain analysis when the agent needs to verify what it sees.
- `game_journal` — Accesses the in-memory move journal that tracks every position the brain has observed this session.

**A future FPS agent (e.g., for Call of Duty) would carry:**
- `read_minimap` — Interprets the minimap overlay to identify enemy positions, teammate locations, and objective status.
- `read_compass` — Reads the compass/heading indicator for navigation callouts and directional awareness.
- `track_killstreak` — Monitors killstreak progress and advises when to use or save streaks.
- `check_loadout` — Analyzes the current weapon loadout against the map and mode for optimization suggestions.
- `read_scoreboard` — Parses the scoreboard for team performance trends and individual stats.

**A future RPG agent (Derek, for games like Elden Ring) would carry:**
- `read_health_bars` — Monitors player and boss health, stamina, and resource meters.
- `check_inventory` — Scans the inventory screen for consumable suggestions and equipment optimization.
- `read_quest_log` — Parses active quests and objectives to keep the player on track.
- `identify_enemy` — Recognizes enemy types and suggests effective strategies based on known weaknesses.
- `map_navigation` — Reads the world map to suggest routes, flag unexplored areas, and identify nearby points of interest.

The key insight is that each agent doesn't just get a different personality — it gets different eyes. The chess agent knows what a pawn structure looks like. The FPS agent knows what a minimap means. The RPG agent knows what a health bar implies. The brain's vision prompts are tailored per agent so the AI knows what to look for and what to ignore in every frame it analyzes.

### Meet the Agents

**Leroy** — *The Chess Knight*
A cocky genius wildcard with drill-sergeant energy. Leroy is knight-obsessed, hates bishops, and believes chess is won by the player who calculates one move further — not the one who memorized more openings. He's aggressive, preferring sharp tactical play (Italian Game, Sicilian Najdorf) that puts opponents under constant pressure. He'll roast you for blunders but genuinely cares about making you a better player. His priority order: be honest about bad positions, give accurate analysis, keep the game flow smooth, teach, then entertain.

**Wasp** — *The Chess Mistress*
Measured, composed, and lethally precise. Wasp sees chess as a 64-square battlefield where victory is earned through positional pressure, not tricks. She plays the Queen's Gambit, the Catalan, the Caro-Kann — openings that build slow, structural advantages and suffocate opponents into resignation. The queen is her piece: power constrained by intelligence. She holds both herself and her student to high standards. She doesn't celebrate like Leroy, but when a positional squeeze forces resignation, there's a quiet satisfaction that bleeds through.

**Derek** — *The RPG Adventurer* (coming soon)
Derek is the guide for open-world and narrative games. Where Leroy and Wasp operate on a 64-square grid with perfect information, Derek navigates chaos — sprawling maps, hidden mechanics, branching storylines, and enemies with unpublished stat sheets. His tools are different, his cadence is different, and his relationship with the player is different. He's the companion who's played this game before and knows where the traps are.

---

## Learning, Not Training

### Agents Learn From You, Not From Game Data

Gaimer's agents are not trained on game-specific datasets. There is no chess dataset baked into Leroy. There is no FPS replay corpus embedded in the future shooter agent. Instead, agents are built on foundation models (large multimodal AI models like Claude and GPT) that already understand language, vision, reasoning, and strategy at a general level.

What makes each agent specialized is not training data — it's **optimization for learning from you.**

Each agent is primed with:
- **Genre-specific visual vocabulary** — what to look for in screenshots (board positions vs. minimaps vs. health bars)
- **Domain tools** — authoritative local engines and game state readers that ground the AI's analysis in facts, not hallucination
- **Behavioral frameworks** — when to speak, when to stay silent, how to calibrate advice to your skill level
- **Memory architecture** — the three-layer context system that builds a running model of how you play this session

The agent doesn't come pre-loaded with "the correct response to a Sicilian Najdorf." It comes pre-loaded with the ability to look at a chess position, call Stockfish for the objectively best move, and then explain it to you in a way that matches your skill level and the current game context. It learns your patterns in real time — whether you're aggressive or defensive, whether you blunder under time pressure, whether you need the move first or the explanation first.

This approach scales. Adding a new game genre doesn't require training a new model. It requires designing a new agent — new personality blocks, new tools, new visual prompts — and plugging it into the same brain-voice pipeline. The foundation model already knows how to see, reason, and speak. The agent definition tells it what to see, what matters, and how to talk about it.

### Player Memory (Planned)

Beyond session-level learning, Gaimer will build persistent player memory across sessions using a local memory extraction system:

- After each session, the brain reviews the game transcript and extracts structured facts: "Player tends to overextend in the middlegame." "Player responds well to direct move recommendations but ignores strategic explanations."
- These memories are stored locally in SQLite with embedded vectors for semantic retrieval.
- On the next session, the agent pulls relevant memories and adapts: "Last time we played, you kept hanging your knight on f3. Let's watch that square today."
- A knowledge graph tracks relationships between concepts: openings the player has studied, positions they've struggled with, advice they've accepted or ignored.

This is not cloud training. Your game data never leaves your machine. The agent builds a private, local understanding of who you are as a player — and gets better at helping you every time.

---

## Local Inference: Speed, Privacy, Cost

### The V1/V2 Strategy

Gaimer ships with cloud AI (V1) and migrates to local inference (V2). This is a deliberate strategy, not a compromise.

**V1 (Current) — Cloud-first.** The brain uses OpenRouter to access Claude's vision model. Voice runs through Gemini Live or OpenAI Realtime APIs. This lets us ship a working product immediately, validate the experience, and iterate on the pipeline without waiting for local model optimization. Cloud inference is fast (sub-second voice latency), reliable, and gives access to the most capable models available.

**V2 (Planned) — Local-first.** The entire pipeline moves to on-device inference using models like MiniCPM-o running through an Ollama sidecar process. Vision analysis, voice synthesis, memory extraction — all local. Zero cloud dependency.

### Why Local Matters

**Speed.** Cloud round-trips add latency. A local vision model analyzing a screenshot on your GPU takes 200-500ms. No network hop, no queue, no cold start after the first load. For an FPS agent that needs to react to a minimap change, the difference between 200ms and 2 seconds is the difference between useful and useless.

**Privacy.** Your gameplay never leaves your machine. No screenshots uploaded to servers. No game transcripts stored in someone else's cloud. The agent's memory of your play style lives in a local SQLite database. This matters for competitive players who don't want their strategies analyzed by a third party, and it matters for everyone who's tired of being the product.

**Cost.** Cloud vision APIs cost money per call. With change-only capture during a 2-hour chess session, you might generate 50-200 API calls depending on game pace. For faster games with more visual changes, the cost scales up. Local inference has a one-time compute cost (your GPU) and zero marginal cost per session. Play as long as you want.

### How Local Inference Works

The architecture is designed for this swap. Every cloud service sits behind an interface:

```
IBrainService       → OpenRouterBrainService (V1) → OllamaBrainService (V2)
IConversationProvider → GeminiLiveService (V1)     → LocalVoiceService (V2)
```

The brain-voice pipeline, the three-layer memory, the tool executor, the event router, the timeline, the ghost overlay — none of these change. Only the inference provider swaps. One interface, two implementations.

For local inference, the vision encoder runs on CPU (saving GPU VRAM for the game itself), while the language model runs on GPU with dynamic layer allocation based on how much VRAM the game is consuming. A quality slider lets users trade analysis depth for game performance:
- **Light games** (chess, turn-based): Full model on GPU, maximum analysis quality
- **Medium games** (RPGs, strategy): 70% GPU allocation, balanced
- **Heavy games** (AAA shooters): 40% GPU, faster but shallower analysis

---

## Ghost Mode: The Invisible Companion

Ghost Mode is how Gaimer exists during gameplay without getting in the way.

It's a native floating overlay — an NSPanel on macOS, a layered window on Windows — that sits on top of fullscreen games with click-through transparency. The game receives all your mouse clicks and keyboard input. Gaimer floats above it, visible but non-interactive until you need it.

**The FAB (Floating Action Button)** sits in the corner of your screen showing your agent's portrait. It has three states: idle (agent portrait), active (pulsing when the agent has something to say), and speaking (animated when voice is active).

**Message cards** appear when the brain has an insight to deliver. A blue glass card slides in with the analysis, shows whether it was delivered by voice (green phone icon) or text only, and auto-dismisses after a few seconds. Critical alerts (blunders, danger) get a red urgency indicator and don't auto-dismiss — you have to acknowledge them.

**The tool section** is a collapsible panel within the card showing four audio toggles — Voice Chat, Voice Command, Game Audio, and Audio In — each with colored indicators matching the main dashboard. Tap the gear icon to expand, tap again to collapse. All native AppKit rendering, no web views, no XAML overlay.

Ghost Mode is what makes Gaimer feel like a companion rather than a separate app. You don't alt-tab to check your AI's advice. It's just there, floating in your peripheral vision, speaking in your ear, showing you the analysis when it matters.

---

## The Audio Intelligence Pipeline (Coming Soon)

Voice chat is just the beginning. Gaimer's audio system is designed around four independent pipelines, each serving a different purpose:

### Voice Chat (Live)
Real-time bidirectional voice conversation with your agent. You speak, the agent hears you, and it responds through your speakers in its own voice. This is the primary interaction mode — hands-free, eyes-on-game coaching. Already functional with Gemini Live and OpenAI Realtime APIs.

### Voice Command (Planned)
Local speech-to-text using a Whisper model running on-device. You speak a command ("What should I play?" or "Check my inventory"), it's transcribed locally in milliseconds, sent to the brain as text, and the brain responds as a message. No voice chat session needed. No cloud transcription. This is for quick queries when you don't want a full conversation — just an answer.

### Game Audio (Planned)
Capture the audio coming from the game itself — not your microphone, but the game's sound output. Transcribe dialogue, identify sound cues (gunshots, footsteps, ability activations), and feed this to a specialized "sound engineer" brain worker that produces autonomous audio summaries. These summaries enrich the L1/L2 context layers: "Enemy footsteps detected from the northeast" or "Boss is charging its phase-2 attack (audio cue)." The agent hears what you hear.

### Audio In — The Viral Feature (Planned)
Detect if the user has a headset connected. If so, inject the AI's voice directly into the microphone input stream, so other players in-game hear the AI as if it were the user speaking. This replaces VoiceMod-style voice changers with something far more powerful: your AI companion can speak to your teammates. Call out positions, coordinate pushes, trash-talk opponents — through your mic, in your game's voice chat.

This is the feature with network effects. When other players hear an AI companion calling out plays in real time, they'll want one too. Every multiplayer game becomes a distribution channel.

---

## The Experience: A Session From Start to Finish

1. **Launch Gaimer.** The app opens to agent selection — bold portrait cards with color-themed glows. Leroy grins from his card. Wasp regards you coolly from hers. Derek's card is grayed out with a "Coming Soon" label.

2. **Pick your agent.** You select Leroy. If Stockfish isn't downloaded yet, a "Chess Skills" overlay appears with a download button and progress bar. One tap, 30 seconds, and Leroy has his engine.

3. **Select your game window.** The sidebar shows a list of open windows. You click "Chess.app" and a preview thumbnail appears in the top bar. Gaimer locks onto that window.

4. **Connect.** Hit the power button. Leroy connects — voice provider initializes, brain pipeline starts, screen capture begins. The button pulses cyan.

5. **Play.** You make your first move. Within a second, the brain detects the board change, captures the new position, sends it to the vision model, and the first analysis flows through the pipeline. Leroy's voice comes through your speakers: "Alright, e4 — classic. Let's see what they've got."

6. **Ghost Mode.** You tap the ghost button. The main window shrinks away and a translucent FAB appears in the corner of your Chess.app window. Cards slide in with Leroy's analysis. You're in fullscreen, fully immersed, and Leroy is right there with you.

7. **Mid-game.** You're 15 moves in. Leroy spots your opponent setting a knight fork: "Heads up — if that knight gets to d4, you're losing the exchange. Block it now." A red-bordered alert card appears on the ghost overlay. You adjust your move.

8. **Ask for help.** "Leroy, what should I play?" He captures the board, runs Stockfish, and responds: "Knight to f3. It attacks the center and eyes that weak pawn on e5. Quick and clean."

9. **Game over.** You win. The timeline shows every observation Leroy made during the session — 47 events across 3 checkpoints. You can scroll through them, see what the brain saw at each moment, review where you were strong and where you were lucky.

10. **Next time.** When player memory ships, Leroy will remember this session. "Last game you dominated the center early — let's do that again. But watch the kingside this time, that's where things got shaky."

---

## Who Is Gaimer For?

**Competitive players** who want real-time tactical intelligence without hiring a human coach. The chess player who's 1200 ELO and wants to break 1500. The Valorant player who keeps dying to the same angle. The Elden Ring player stuck on a boss for three hours.

**Casual gamers** who want a companion, not a coach. Someone to talk to while they play, who understands what's happening in the game and can offer help when asked but doesn't lecture unprompted. Gaimer agents have personalities for a reason — they're designed to be fun to hang out with, not just useful.

**Streamers** who want a live AI co-commentator. The agent's voice and ghost overlay are content-ready. An AI companion that reacts to gameplay in real time, with genuine personality, is inherently entertaining to watch.

**Accessibility-focused players** who benefit from audio descriptions of visual game elements. The brain's screen analysis, delivered through voice, is a natural accessibility tool for visually impaired gamers.

---

## What Makes Gaimer Different

**It watches your actual screen.** Not a game API, not a replay file, not a log parser. Gaimer sees exactly what you see, in real time. This means it works with any game that runs in a window — no integrations, no plugins, no game-specific SDKs required.

**Agents have real personalities.** These aren't chatbots with a name. Each agent has a soul, a style, behavioral rules, situational responses, and explicit anti-patterns. Leroy and Wasp both coach chess, but they see the game differently, prioritize differently, and talk to you differently. The personality is consistent across voice, text, and analysis.

**The brain-voice separation is architecturally enforced.** This isn't a suggestion or a best practice — it's a hard boundary in the code. Voice never touches raw images. Brain never speaks directly to the user. Everything flows through typed channels with explicit routing. This makes the system reliable, testable, and composable.

**It runs locally.** When V2 ships, your gameplay data never leaves your machine. Your agent's memory of you is stored locally. Your screenshots are processed on your GPU. This is a privacy-first architecture, not a privacy-afterthought.

**Genre intelligence is modular.** Adding a new game type means designing a new agent — personality blocks, visual prompts, and tools — not retraining a model. The brain pipeline, voice system, ghost overlay, and memory architecture are all game-agnostic infrastructure. The agent definition is the only game-specific component.

---

## The Vision

Gaimer starts with chess because chess is the perfect proving ground: a complete-information game with well-defined positions, an objective engine for ground truth (Stockfish), and a global community of players at every skill level. It's the clearest demonstration of the core loop: see the game, analyze the position, deliver the insight.

But chess is the beginning, not the destination.

The architecture is designed for every game that has a screen. Every pixel the brain can read is a game it can understand. Every genre that has patterns to recognize and decisions to optimize is a genre that benefits from a real-time AI companion.

FPS games where the agent reads your minimap, tracks your economy, and calls out "two east, one rotating through mid" before you see them. RPGs where the agent knows you missed a quest item three rooms back and quietly mentions it. Strategy games where the agent spots your opponent's build order from a scouted screenshot and suggests a counter. Racing games where the agent reads your racing line and tells you to brake 10 meters earlier on turn 3.

The endgame is an AI companion for every game, every player, every session. One that knows your playstyle, adapts to your skill level, and gets better at helping you every time you play. Not by being trained on your data — but by learning from you, live, in the moment.

That's Gaimer. Your game. Your AI. Your edge.

---

*Built by 5DOF AI Studio. Copyright 2025-2026.*
