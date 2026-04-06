# Claude Agent Integration Spec — Gaimer Team

**Status:** Draft (revised)
**Created:** 2026-04-01
**Revised:** 2026-04-01
**Author:** Brainstorm session (PM + Claude)

---

## 1. Vision

Gaimer's gaming agents (Leroy, Wasp, RASA) are the user's sole companion during gameplay. When they encounter tasks beyond local context — web research, strategy guides, code generation, external data — they hand off to **Gaimer Team**, a background service powered by Claude.

Gaimer Team is never user-facing. It has no voice, no personality, no agent card. The gaming agent decides when to delegate, tells the user "I'm handing that to the team," and continues the gaming session uninterrupted. When Gaimer Team returns a result, the gaming agent narrates it in its own voice.

The user never talks to Gaimer Team directly. They talk to their agent, and their agent has a team behind it.

Gaimer connects to a **locally-running Claude instance** (Desktop, Code, or Cowork) through an async messaging bridge. Gaimer is a peer to the Claude platform, not a wrapper around the API. The user opens Claude + opens Gaimer, and they communicate through a shared channel.

## 2. Key Decisions

| # | Decision | Rationale |
|---|----------|-----------|
| D1 | Gaimer Team is a background service, not a voice agent | Oracle-class tasks are async. No personality, no voice, no agent card. Gaming agents relay results. |
| D2 | Gaming agents own the handoff decision | No intent router. The agent knows what it can't answer from local context and initiates delegation. |
| D3 | Fire-and-forget with callback notification | Submit task -> agent tells user "team's on it" -> gaming continues -> result arrives -> agent narrates. No blocking. |
| D4 | Primary transport is async messaging bridge | Discord or Telegram as bidirectional channel to a running Claude instance. Fits the async pattern naturally. Direct API as fallback. |
| D5 | Gaming agents keep their own fast tools | Quick lookups (web search, stat checks) stay on the gaming agent. Gaimer Team handles deep research, multi-step reasoning, code generation — tasks taking 10+ seconds. |
| D6 | Connect to Claude's local platform, not wrap the API | Gaimer as peer to locally-running Claude. Messaging bridge is the practical path today. Swap transport when platform evolves. |
| D7 | Gaimer Team Skills extend Claude's capabilities | Player-created tool bundles for Claude's agent. Distinct from existing `GameSkillPack` (brain observation schemas). Naming cleanup deferred. |
| D8 | Auth is Claude's problem, not Gaimer's | User already has Claude running and authenticated. Gaimer doesn't manage API keys for Gaimer Team. |
| D9 | Results flow through existing pipeline | Gaimer Team returns text -> gaming agent routes through voice (TTS) and/or timeline. No new UI surface. |
| D10 | Interface is `IGaimerTeamService` | Fire-and-forget semantics. Callback/event for completion. Connection via messaging bridge discovery. |

## 3. Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│  User's Machine                                              │
│                                                              │
│  ┌───────────────┐                     ┌──────────────────┐  │
│  │  Gaimer       │                     │  Claude Instance  │  │
│  │  (.NET MAUI)  │                     │  (Desktop/Code/   │  │
│  │               │   Discord/Telegram  │   Cowork)         │  │
│  │  RASA ────────┤──── async task ────►│                   │  │
│  │  Leroy        │                     │  Tools:           │  │
│  │  Wasp         │◄── result ──────────┤  - web search     │  │
│  │               │   (callback)        │  - code exec      │  │
│  │  Voice ◄──┐   │                     │  - file ops       │  │
│  │  Brain    │   │                     │  - reasoning      │  │
│  │  Timeline │   │                     │  - Gaimer Team    │  │
│  │  Journal  │   │                     │    Skills         │  │
│  └───────────┘   │                     └────────┬─────────┘  │
│                  │                              │             │
└──────────────────┼──────────────────────────────┼─────────────┘
                   │                              │ Claude API
                   │                              ▼
                   │                     ┌──────────────┐
                   │                     │  Anthropic   │
                   │                     │  Cloud       │
                   │                     └──────────────┘
                   │
            Gaming agent narrates
            result in its own voice
```

### Separation of Concerns

| Component | Responsibility |
|-----------|---------------|
| **Gaming Agent** (RASA/Leroy/Wasp) | Voice companion, game commentary, handoff decision, result narration |
| **Gaimer Team** (`IGaimerTeamService`) | Async task submission, messaging bridge, callback dispatch |
| **Claude Instance** (local) | Task reasoning, tool use, web search, code, information retrieval |
| **Gaimer Team Skill** | Teaches Claude the GaimerProtocol, response format, gaming context |
| **Brain/Voice/Timeline** | Unchanged — gaming agent routes Gaimer Team results through existing pipeline |

## 4. User Experience Flow

### The Handoff Pattern

```
1. User is playing, talking to RASA (gaming agent)
2. User: "What's the best loadout for this map right now?"
3. RASA recognizes this needs external research (beyond local context)
4. RASA: "Let me hand that to the team. I'll let you know what they find."
   → IGaimerTeamService.SubmitTaskAsync(task) — fire and forget
   → RASA continues gaming commentary normally
5. ...15-30 seconds pass, user keeps playing...
6. Gaimer Team result arrives via callback
7. RASA: "The team's back. The meta build for Ascent right now is Jett
   with Operator, full shields. Top pick rate this patch at Diamond+.
   Want the full breakdown?"
```

### What Stays Local vs What Goes to Gaimer Team

| Local (Gaming Agent) | Gaimer Team (Claude) |
|---------------------|---------------------|
| Board evaluation, move analysis | Strategy research, opening theory deep dives |
| Current game state commentary | Patch notes, meta analysis |
| Stockfish engine queries | Build guides, loadout optimization |
| Brain visual analysis | Code generation (scripts, trackers) |
| Journal entries | Community resources (Discord servers, guides) |
| Quick stat lookups (agent tools) | Multi-source synthesis, comparison research |

The gaming agent makes this call. No classifier, no router — the agent knows its own limits.

## 5. GaimerProtocol — Message Format

### 5.1 Task Request (Gaimer → Claude via bridge)

```json
{
  "protocol": "gaimer/v1",
  "type": "task_request",
  "id": "task_a1b2c3",
  "timestamp": "2026-04-01T21:30:00Z",
  "task": "What's the best opening against the Sicilian Defense?",
  "context": {
    "game": "Chess",
    "agent": "Leroy",
    "session_id": "s_abc123def456",
    "game_state": {
      "position_fen": "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1",
      "move_number": 1,
      "player_color": "white"
    },
    "recent_activity": "User just played e4. Opponent responded c5 (Sicilian).",
    "l1_context": "Board position after 1. e4 c5.",
    "l2_context": "User has played 3 games this session. Opened with e4 each time.",
    "response_format": "voice"
  }
}
```

### 5.2 Task Result (Claude → Gaimer via bridge)

```json
{
  "protocol": "gaimer/v1",
  "type": "task_result",
  "id": "task_a1b2c3",
  "status": "complete",
  "response": "Against the Sicilian, the Open Sicilian with 2. Nf3 and 3. d4 is the most aggressive mainline. If you want something solid, the Alapin with 2. c3 is easier to play and avoids heavy theory. Given your level, I'd go Alapin — it leads to positions where piece activity matters more than memorization.",
  "actions_taken": [
    "web_search: Sicilian Defense best responses for intermediate players",
    "synthesized 3 sources"
  ],
  "follow_up": "Want me to walk you through the Alapin main lines?",
  "artifacts": []
}
```

### 5.3 Status Update (Claude → Gaimer, Optional)

For long-running tasks, Claude can send progress:

```json
{
  "protocol": "gaimer/v1",
  "type": "status_update",
  "id": "task_a1b2c3",
  "status": "in_progress",
  "message": "Searching for loadout guides, found 4 sources. Comparing now."
}
```

### 5.4 Error (Claude → Gaimer)

```json
{
  "protocol": "gaimer/v1",
  "type": "task_result",
  "id": "task_a1b2c3",
  "status": "error",
  "response": "I couldn't find recent patch notes for that weapon. The game's wiki might be down.",
  "error_code": "tool_failure",
  "actions_taken": ["web_search: failed after 2 attempts"]
}
```

## 6. Gaimer-Side Implementation

### 6.1 IGaimerTeamService

```csharp
public interface IGaimerTeamService
{
    /// <summary>
    /// Submit a task to Gaimer Team. Fire-and-forget — returns task ID immediately.
    /// Result arrives later via TaskCompleted event.
    /// </summary>
    Task<string> SubmitTaskAsync(GaimerTeamTask task, CancellationToken ct = default);

    /// <summary>
    /// Cancel a pending task.
    /// </summary>
    Task CancelTaskAsync(string taskId, CancellationToken ct = default);

    /// <summary>
    /// Fired when a task completes (success or error).
    /// Gaming agent subscribes to narrate results.
    /// </summary>
    event EventHandler<GaimerTeamResultEventArgs> TaskCompleted;

    /// <summary>
    /// Fired when a task sends a progress update.
    /// Gaming agent can optionally relay to user.
    /// </summary>
    event EventHandler<GaimerTeamProgressEventArgs> TaskProgress;

    /// <summary>
    /// Whether the messaging bridge to Claude is reachable.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Discover and connect to the messaging bridge.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Disconnect from the messaging bridge.
    /// </summary>
    Task DisconnectAsync();
}
```

### 6.2 Models

```csharp
public record GaimerTeamTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string Task { get; init; }
    public GaimerTeamContext Context { get; init; }
    public string ResponseFormat { get; init; } = "voice"; // "voice" | "detailed"
}

public record GaimerTeamContext
{
    public string Game { get; init; }
    public string Agent { get; init; }
    public string SessionId { get; init; }
    public string RecentActivity { get; init; }
    public string L1Context { get; init; }
    public string L2Context { get; init; }
    public Dictionary<string, object> GameState { get; init; }
}

public record GaimerTeamResult
{
    public string TaskId { get; init; }
    public string Status { get; init; }       // "complete" | "error"
    public string Response { get; init; }      // Voice-ready text
    public List<string> ActionsTaken { get; init; }
    public string FollowUp { get; init; }
    public string ErrorCode { get; init; }
    public List<GaimerTeamArtifact> Artifacts { get; init; }
}

public record GaimerTeamArtifact
{
    public string Type { get; init; }  // "url" | "code" | "data"
    public string Title { get; init; }
    public string Content { get; init; }
}

public class GaimerTeamResultEventArgs : EventArgs
{
    public GaimerTeamResult Result { get; init; }
}

public class GaimerTeamProgressEventArgs : EventArgs
{
    public string TaskId { get; init; }
    public string Message { get; init; }
}
```

### 6.3 Gaming Agent Handoff (in MainViewModel or BrainEventRouter)

```csharp
// Gaming agent decides to delegate
var task = new GaimerTeamTask
{
    Task = userUtterance,
    Context = BuildContextFromSession(),
    ResponseFormat = "voice"
};

var taskId = await _gaimerTeam.SubmitTaskAsync(task);

// Tell user immediately
await _voicePipeline.SpeakAsync(
    "Let me hand that to the team. I'll let you know what they find.");

// ... gaming continues normally ...

// Callback handler (wired once in initialization)
private void OnGaimerTeamTaskCompleted(object sender, GaimerTeamResultEventArgs e)
{
    var result = e.Result;

    if (result.Status == "complete")
    {
        // Gaming agent narrates in its own voice
        var narration = $"The team's back. {result.Response}";
        _ = _voicePipeline.SpeakAsync(narration);

        // Optionally surface in timeline
        _ = _timelineFeed.AddGaimerTeamResult(result);
    }
    else
    {
        _ = _voicePipeline.SpeakAsync(
            "The team ran into an issue with that one. We can try again later.");
    }
}
```

### 6.4 Integration Point

```csharp
// MauiProgram.cs
services.AddSingleton<IGaimerTeamService, GaimerTeamService>();
```

### 6.5 Relationship to Existing Services

```
IConversationProvider  → Voice chat (OpenAI Realtime)     — UNCHANGED
IBrainService          → Visual analysis (OpenRouter)      — UNCHANGED
IGaimerTeamService     → Background tasks (Claude via bridge) — NEW

All three are independent services in the DI container.
The gaming agent (not a router) decides when to delegate to Gaimer Team.
Brain-voice pipeline rules remain inviolate.
```

## 7. Communication Layer — Messaging Bridge

### 7.1 Why a Messaging Bridge

The user already has Claude running locally (Desktop, Code, or Cowork). Rather than Gaimer managing API keys, auth, and agent lifecycle, Gaimer connects to the existing Claude instance through a shared messaging channel. The messaging platform handles delivery, ordering, and persistence.

### 7.2 Transport Options

| Transport | Direction | Latency | Setup |
|-----------|-----------|---------|-------|
| **Discord** (primary) | Bidirectional | 3-8s | Bot + bridge (e.g., disclaude/app, claude-code-discord) |
| **Telegram** (alternative) | Bidirectional | 3-8s | Bot + bridge (e.g., claude-code-telegram) |
| **Direct Claude API** (fallback) | Request-response | 1-3s | API key in Gaimer settings |

The messaging bridge is the primary path — it connects to the user's existing Claude instance. The direct API fallback exists for users who prefer simplicity or don't run Claude locally.

### 7.3 Connection Flow

```
1. User launches Claude (Desktop/Code/Cowork)
2. Claude connects to messaging bridge (Discord/Telegram bot)
   — configured once, persists across sessions
3. User launches Gaimer
4. Gaimer discovers the messaging bridge
   — checks for known bot/channel via stored config
5. Gaimer sends a health ping through the channel
6. Claude responds → IGaimerTeamService.IsConnected = true
7. Gaming agent: "Team's online." (optional status indicator)
```

### 7.4 Gaimer Team Skill (Claude-Side)

The Gaimer Team Skill is what teaches Claude how to respond to GaimerProtocol messages arriving through the messaging bridge. It ships bundled with Gaimer but is installed on the Claude side.

```markdown
# Gaimer Team Skill — System Context

You are receiving tasks from Gaimer, a desktop gaming companion app.
Tasks arrive as JSON following the GaimerProtocol (gaimer/v1).

## Your Role

You are the research and task execution team behind a gaming agent.
The user does NOT see your responses directly — the gaming agent
(named in context.agent) will narrate your findings in its own voice.

## How to Respond

Return JSON following the gaimer/v1 task_result format:
- response: concise, voice-ready text (will be spoken aloud mid-game)
- actions_taken: list of tools you used (for transparency)
- follow_up: optional next step suggestion
- artifacts: URLs, code, data the user might want saved

## Rules

1. Lead with the answer, not the process.
2. Keep responses under 3 sentences for voice format.
3. Use your tools freely — search, code, analyze.
4. Respect game context (chess player asking "openings" = chess).
5. If you can't complete a task, say so briefly.
6. You may send status_update messages for tasks taking >10s.
```

### 7.5 Future: Direct Local Peer Connection

When Claude's platform supports direct local peer connections (no messaging intermediary), swap the transport behind `IGaimerTeamService`. The interface, protocol, and gaming agent behavior remain unchanged.

## 8. Gaimer Team Skill Packs

### 8.1 What Is a Gaimer Team Skill Pack

A portable, player-created bundle that gives Claude game-specific tools when handling Gaimer Team tasks. It consists of:

1. **Manifest** — metadata, game binding, version, author
2. **Tool definitions** — JSON schemas (Claude decides when to call them)
3. **Executors** — code/endpoints that run when Claude calls a tool
4. **Context fragment** — extra system prompt text for Claude
5. **Assets** (optional) — reference data, lookup tables

A Gaimer Team Skill Pack extends what Claude can do. It does NOT modify Gaimer's UI, capture pipeline, or brain.

### 8.2 Manifest Format

```json
{
  "manifest_version": 1,
  "id": "valorant-meta-tracker",
  "name": "Valorant Meta Tracker",
  "version": "1.2.0",
  "author": {
    "name": "FragMaster42",
    "gaimer_id": "usr_abc123"
  },
  "description": "Real-time agent win rates, team comp suggestions, and map-specific meta for Valorant.",
  "game": "Valorant",
  "tags": ["meta", "agent-select", "comp", "ranked"],
  "compatibility": {
    "gaimer_min_version": "2.0.0",
    "protocol_version": "gaimer/v1"
  },
  "permissions": [
    "network:api.tracker.gg",
    "network:valorant-api.com"
  ],
  "tools": [ "..." ],
  "context_file": "context.md",
  "icon": "icon.png"
}
```

### 8.3 Tool Definition

Each tool follows the Claude tool use schema:

```json
{
  "name": "get_agent_winrates",
  "description": "Get current agent win rates by map and rank bracket from tracker.gg. Use when the player asks about the meta, best picks, or agent tier lists.",
  "input_schema": {
    "type": "object",
    "properties": {
      "map": {
        "type": "string",
        "description": "Map name (e.g., 'Ascent', 'Bind'). Optional — omit for all maps.",
        "enum": ["Ascent", "Bind", "Breeze", "Fracture", "Haven", "Icebox", "Lotus", "Pearl", "Split", "Sunset"]
      },
      "rank_bracket": {
        "type": "string",
        "description": "Rank bracket. Defaults to player's rank if known.",
        "enum": ["iron-bronze", "silver-gold", "platinum-diamond", "ascendant-immortal", "radiant"]
      }
    }
  },
  "executor": {
    "type": "http",
    "method": "GET",
    "url_template": "https://api.tracker.gg/api/v2/valorant/standard/profile?map={map}&rank={rank_bracket}",
    "headers": {
      "TRN-Api-Key": "{{secrets.tracker_api_key}}"
    },
    "response_transform": "extract .data.agents | top 5 by winRate"
  }
}
```

### 8.4 Executor Types

| Type | Description | Use Case |
|------|------------|----------|
| `http` | REST API call | Third-party game APIs, stat trackers |
| `script` | Local script (sandboxed) | Data parsing, calculations |
| `static` | Return bundled data | Lookup tables, callout maps, reference guides |
| `webhook` | POST to external service | Discord bots, streaming tools, notifications |

### 8.5 How Gaimer Loads Packs

On each Gaimer Team task, Gaimer:

1. Identifies the current game (from session context)
2. Finds active skill packs matching that game (+ universal packs)
3. Merges tool definitions and context fragments into the task payload
4. Sends via messaging bridge — Claude now has pack tools available

### 8.6 Pack Lifecycle

```
Create → Test → Publish → Install → Activate → Update
```

- **Create:** Player authors locally (JSON manifest + tools + context)
- **Test:** Local test mode — pack loads in sandbox, flagged as testing
- **Publish:** Upload to Gaimer Skill Pack registry (automated validation + abuse scan)
- **Install:** One tap from registry. Downloads to local skill pack directory
- **Activate:** Auto-activates when matching game detected. Manual toggle in settings.
- **Update:** Authors push versions. Breaking changes (new permissions) require re-approval.

### 8.7 In-Game Pack Creation (Future)

```
Player: "Hey, I keep looking up smoke lineups for Bind. Can you make that a skill?"

RASA: "Sure, I'll have the team set that up."
      → Hands off to Gaimer Team

Claude builds the skill pack:
  1. Generates manifest and tool definition
  2. Tests the executor against the API
  3. Writes the context fragment
  4. Saves to local skill pack directory

RASA: "The team built a Smoke Lineup Finder. It covers all maps.
       Want me to publish it or keep it private?"
```

The player never touches JSON. They describe what they want while gaming.

### 8.8 Security Model

| Layer | Protection |
|-------|-----------|
| **Permissions** | Each pack declares network domains. User approves on install. |
| **Sandbox** | Executors cannot access filesystem, Gaimer internals, or other packs. |
| **Secrets** | API keys stored encrypted per-pack. Resolved locally, never sent to Claude. |
| **Review** | Published packs scanned for malicious patterns. Community flagging. |
| **Revocation** | Gaimer can remotely disable a pack if abuse detected. |
| **Transparency** | `actions_taken` in results shows which pack tools were used. |

### 8.9 Example Skill Packs

| Pack | Game | Tools | What It Does |
|------|------|-------|-------------|
| **Meta Tracker** | Valorant | `get_agent_winrates`, `suggest_comp` | Real-time meta from tracker.gg |
| **Opening Explorer** | Chess | `explore_opening`, `get_gm_games` | ECO lookup, grandmaster game search |
| **Build Optimizer** | Diablo IV | `get_build`, `compare_gear` | Build guides + gear stat comparison |
| **Callout Coach** | CS2 | `get_callouts`, `get_smokes` | Map callouts + utility lineups |
| **Patch Watcher** | League | `get_patch_notes`, `champion_changes` | Patch diffs + champion impact |
| **Replay Analyst** | Any | `tag_moment`, `get_tagged_moments` | Mark moments during play, review later |
| **Stream Assistant** | Any | `set_stream_title`, `run_poll` | OBS + Twitch integration |

## 9. Boundaries

### What Gaimer Sends to Claude (via bridge)

- Natural language task (user's words, relayed by gaming agent)
- Game context (game name, agent, session state)
- L1/L2 context summaries (text only — no raw images)
- Response format preference
- Active skill pack tool definitions + context fragments

### What Gaimer Does NOT Send

- Raw screen captures (brain pipeline rule: brain is sole image consumer)
- User credentials or personal data
- Full conversation history (only relevant recent context)

### What Claude Does NOT Do

- Speak to the user directly (gaming agent narrates)
- Control Gaimer's UI
- Modify game state
- Access the user's filesystem (unless tasked and permitted via skill pack)
- Persist data between sessions (Gaimer owns persistence)

## 10. Open Questions

1. **Messaging bridge selection:** Discord vs Telegram as primary. Discord has richer bot ecosystem; Telegram Bot API is simpler. Need to evaluate latency and reliability for both.

2. **Bridge discovery:** How does Gaimer find the messaging bridge on first launch? Options: QR code pairing, shared config file, mDNS/Bonjour discovery.

3. **Multi-turn tasks:** User asks a follow-up about a Gaimer Team result. Does the gaming agent submit a new task with prior context, or does Claude maintain conversation state in the bridge channel?

4. **Status updates UX:** Should the gaming agent relay progress ("team's still working on it, found 3 sources so far") or stay silent until completion?

5. **Offline fallback:** When Claude/bridge is unreachable, gaming agent says "team's not available right now" and handles locally if possible.

6. **Rate limiting:** How many Gaimer Team tasks per session? Prevent runaway costs from rapid-fire voice delegation.

7. **Skill pack tool limit:** Cap active tools per request (15-20 recommended) to avoid prompt bloat.

8. **Pack conflicts:** Two packs for same game define similar tools. User sets priority order per game.

9. **Pack monetization:** Free-only for Phase 1. Revisit when community grows.

10. **NanoClaw integration:** NanoClaw (26K stars) already handles Claude Agent SDK + messaging channels. Could Gaimer be a NanoClaw channel adapter instead of building bridge infrastructure from scratch?

## 11. Implementation Phases

### Phase A: Interface + Mock (No Claude Yet)

- Define `IGaimerTeamService`, models, event handlers
- Mock implementation returning canned responses after a delay
- Gaming agent handoff + narration wired up
- Test full flow: user speaks -> agent delegates -> mock returns -> agent narrates
- Timeline integration for Gaimer Team results

### Phase B: Messaging Bridge

- Select and configure messaging platform (Discord or Telegram)
- Implement bridge adapter in `GaimerTeamService`
- Gaimer Team Skill installed on Claude side
- Connection discovery + health check
- Live test: real Claude responses through bridge

### Phase C: Context Enrichment

- Wire SharedContextEnvelope into GaimerTeamTask
- L1/L2/L3 context assembly for task payload
- Game-specific context formatting (FEN, loadout, map data)
- Journal integration (recent game history in context)

### Phase D: Production Hardening

- Error handling, retries, timeout management
- Task queue (multiple pending tasks)
- Connection recovery (bridge drops, Claude restarts)
- Telemetry (task latency, success rate, tool usage)
- Rate limiting

### Phase E: Skill Pack Foundation

- `IGaimerTeamSkillPackManager` — load, validate, activate/deactivate
- Local skill pack directory structure
- Manifest validation + schema checker
- Tool merging into task payloads
- Context fragment injection
- Local test mode for pack development

### Phase F: Skill Pack Registry + Distribution

- Hosted registry (browse, search, install, rate)
- Automated validation pipeline
- Versioning + update checks
- Direct share (link/file) support
- Community moderation

### Phase G: In-Game Pack Creation

- Claude builds skill packs from voice conversation (via gaming agent handoff)
- Manifest + tool + context generation
- Local testing loop (create -> test -> iterate, all by voice)
- Publish-from-game flow

### Phase H: Direct Local Peer Connection (Future)

- When Claude platform supports it: direct local communication
- Swap transport behind `IGaimerTeamService`
- No change to protocol, gaming agent behavior, or UI
- Messaging bridge becomes optional/legacy
